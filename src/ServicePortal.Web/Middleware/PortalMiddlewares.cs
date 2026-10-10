using Microsoft.AspNetCore.Identity;
using ServicePortal.Domain.Entities;

namespace ServicePortal.Web.Middleware;

public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    public const string CorrelationHeaderName = "X-Correlation-Id";

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
    {
        var correlationId = context.Request.Headers[CorrelationHeaderName].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        context.Items[CorrelationHeaderName] = correlationId;
        context.Response.Headers[CorrelationHeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["User"] = context.User.Identity?.Name ?? "Anonymous"
        }))
        {
            await _next(context);
        }
    }
}

public class UserActiveCheckMiddleware
{
    private readonly RequestDelegate _next;

    public UserActiveCheckMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var user = await userManager.GetUserAsync(context.User);
            if (user != null && !user.IsActive)
            {
                // Inactive user denied access even if credentials/cookies are valid (Section 6.1)
                await signInManager.SignOutAsync();
                context.Response.Redirect("/Account/Deactivated");
                return;
            }
        }

        await _next(context);
    }
}
