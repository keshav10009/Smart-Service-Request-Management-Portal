using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ServicePortal.Application.Interfaces;
using ServicePortal.Application.Services;
using ServicePortal.Domain.Entities;
using ServicePortal.Infrastructure.BackgroundJobs;
using ServicePortal.Infrastructure.Data;
using ServicePortal.Infrastructure.Services;
using ServicePortal.Web.Middleware;

var builder = WebApplication.CreateBuilder(args);

// 1. Database Configuration with SQL Server and SQLite fallback
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost;Database=ContosoServicePortal;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

var dbProvider = builder.Configuration["DatabaseProvider"] ?? "SqlServer";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (dbProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite("Data Source=ContosoServicePortal.db");
    }
    else
    {
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
        });
    }
});

// 2. ASP.NET Core Identity with Security & Lockout Policies (Section 6.1 & 7.1)
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;

    // Lockout settings on repeated failed attempts
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// 3. Application Services Registration
builder.Services.AddSingleton<ISlaCalculator, SlaCalculator>();
builder.Services.AddSingleton<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IReferenceNumberGenerator, ReferenceNumberGenerator>();
builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IServiceRequestService, ServiceRequestService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

// 4. Background Workers (Section 6.3 & 6.4)
builder.Services.AddHostedService<SlaBreachHostedService>();
builder.Services.AddHostedService<AutoCloseResolvedHostedService>();

// 5. MVC & Antiforgery
builder.Services.AddControllersWithViews();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});

var app = builder.Build();

// 6. Database Migration and Seeding
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        try
        {
            await context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning("MigrateAsync encountered notice (falling back to EnsureCreated): {Msg}", ex.Message);
            await context.Database.EnsureCreatedAsync();
        }

        await DbSeeder.SeedAsync(context, userManager, roleManager);
        logger.LogInformation("Database initialized and demo data seeded successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred during database migration/seeding.");
    }
}

// 7. HTTP Request Pipeline & Security Middleware
app.UseMiddleware<CorrelationIdMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseMiddleware<UserActiveCheckMiddleware>();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();

// For Integration Test WebApplicationFactory support
public partial class Program { }
