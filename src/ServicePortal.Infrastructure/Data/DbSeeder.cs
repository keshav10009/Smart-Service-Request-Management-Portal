using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ServicePortal.Domain.Entities;
using ServicePortal.Domain.Enums;
using ServicePortal.Infrastructure.Data;

namespace ServicePortal.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        // 1. Roles
        foreach (var roleName in AppRoles.AllRoles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        // 2. Support Teams
        if (!await context.SupportTeams.AnyAsync())
        {
            var teams = new List<SupportTeam>
            {
                new() { Name = "IT Support Services", Description = "Laptops, workstations, network, security, and developer software." },
                new() { Name = "Facilities & Maintenance", Description = "Building access, desk equipment, electrical, HVAC, and office repairs." },
                new() { Name = "Human Resources", Description = "Payroll, benefits, employee onboarding, policies, and workplace culture." },
                new() { Name = "General Support", Description = "General office inquiries, stationery, and travel support." }
            };
            context.SupportTeams.AddRange(teams);
            await context.SaveChangesAsync();
        }

        var itTeam = await context.SupportTeams.FirstAsync(t => t.Name == "IT Support Services");
        var facilitiesTeam = await context.SupportTeams.FirstAsync(t => t.Name == "Facilities & Maintenance");
        var hrTeam = await context.SupportTeams.FirstAsync(t => t.Name == "Human Resources");
        var generalTeam = await context.SupportTeams.FirstAsync(t => t.Name == "General Support");

        // 3. Categories
        if (!await context.Categories.AnyAsync())
        {
            var categories = new List<Category>
            {
                new() { Name = "Hardware & Workstations", Description = "Laptops, monitors, keyboards, docks.", SupportTeamId = itTeam.Id },
                new() { Name = "Software & Applications", Description = "Operating systems, IDEs, licensed tools, and updates.", SupportTeamId = itTeam.Id },
                new() { Name = "Network & VPN Access", Description = "Office Wi-Fi, remote VPN, firewall, and DNS.", SupportTeamId = itTeam.Id },
                new() { Name = "Office Facilities & Ergonomics", Description = "Desks, chairs, whiteboards, lighting, and HVAC.", SupportTeamId = facilitiesTeam.Id },
                new() { Name = "Payroll & Benefits", Description = "Salary statements, tax deductions, and insurance claims.", SupportTeamId = hrTeam.Id },
                new() { Name = "Onboarding & Badging", Description = "New employee kits, access cards, and identity passes.", SupportTeamId = hrTeam.Id },
                new() { Name = "General Office Inquiries", Description = "General assistance, stationery, and front desk questions.", SupportTeamId = generalTeam.Id }
            };
            context.Categories.AddRange(categories);
            await context.SaveChangesAsync();
        }

        // 4. SLA Policies
        if (!await context.SlaPolicies.AnyAsync())
        {
            var policies = new List<SlaPolicy>
            {
                new() { Priority = RequestPriority.Critical, ResponseTargetHours = 1, ResolutionTargetHours = 4, IsActive = true },
                new() { Priority = RequestPriority.High, ResponseTargetHours = 4, ResolutionTargetHours = 8, IsActive = true },
                new() { Priority = RequestPriority.Medium, ResponseTargetHours = 8, ResolutionTargetHours = 24, IsActive = true },
                new() { Priority = RequestPriority.Low, ResponseTargetHours = 48, ResolutionTargetHours = 96, IsActive = true }
            };
            context.SlaPolicies.AddRange(policies);
            await context.SaveChangesAsync();
        }

        // 5. Seed Users
        const string defaultPassword = "Pass@123";

        // Admin
        var admin = await EnsureUserAsync(userManager, "admin@contoso.com", "Admin User", "IT Operations", true, true, null);
        await userManager.AddToRoleAsync(admin, AppRoles.Administrator);

        // Team Leads
        var itLead = await EnsureUserAsync(userManager, "lead.it@contoso.com", "Marcus Vance (IT Lead)", "IT Operations", true, true, itTeam.Id);
        await userManager.AddToRoleAsync(itLead, AppRoles.TeamLead);

        var hrLead = await EnsureUserAsync(userManager, "lead.hr@contoso.com", "Elena Rostova (HR Lead)", "Human Resources", true, true, hrTeam.Id);
        await userManager.AddToRoleAsync(hrLead, AppRoles.TeamLead);

        // Support Agents
        var itAgent = await EnsureUserAsync(userManager, "agent.john@contoso.com", "John Carter (IT Agent)", "IT Operations", true, true, itTeam.Id);
        await userManager.AddToRoleAsync(itAgent, AppRoles.SupportAgent);

        var facilitiesAgent = await EnsureUserAsync(userManager, "agent.sarah@contoso.com", "Sarah Jenkins (Facilities)", "Facilities", true, true, facilitiesTeam.Id);
        await userManager.AddToRoleAsync(facilitiesAgent, AppRoles.SupportAgent);

        // Requesters
        var verifiedRequester = await EnsureUserAsync(userManager, "requester1@contoso.com", "Alice Smith (Requester)", "Finance", true, true, null);
        await userManager.AddToRoleAsync(verifiedRequester, AppRoles.Requester);

        var unverifiedRequester = await EnsureUserAsync(userManager, "unverified@contoso.com", "Bob Unverified (Demo)", "Sales", true, false, null);
        await userManager.AddToRoleAsync(unverifiedRequester, AppRoles.Requester);

        // 6. Reference Sequence initialization
        var currentYear = DateTime.UtcNow.Year;
        var seq = await context.ReferenceSequences.FirstOrDefaultAsync(s => s.Year == currentYear);
        if (seq == null)
        {
            seq = new ReferenceSequence { Year = currentYear, LastNumber = 107 };
            context.ReferenceSequences.Add(seq);
            await context.SaveChangesAsync();
        }

        // 7. Seed Sample Service Requests
        if (!await context.ServiceRequests.AnyAsync())
        {
            var hwCategory = await context.Categories.FirstAsync(c => c.Name.StartsWith("Hardware"));
            var netCategory = await context.Categories.FirstAsync(c => c.Name.StartsWith("Network"));
            var facCategory = await context.Categories.FirstAsync(c => c.Name.StartsWith("Office"));
            var payCategory = await context.Categories.FirstAsync(c => c.Name.StartsWith("Payroll"));

            var now = DateTime.UtcNow;

            // Request 1: In Progress with comments and history
            var req1 = new ServiceRequest
            {
                ReferenceNumber = $"SR-{currentYear}-000101",
                Title = "Laptop display flickering after dock connection",
                Description = "Whenever I connect my Dell Precision workstation to the dual-display USB-C dock in office booth 4B, the secondary screen flickers violently.",
                RequesterId = verifiedRequester.Id,
                CategoryId = hwCategory.Id,
                SupportTeamId = itTeam.Id,
                AssigneeId = itAgent.Id,
                Priority = RequestPriority.High,
                Status = RequestStatus.InProgress,
                PreferredContactMethod = PreferredContactMethod.Portal,
                CreatedAt = now.AddHours(-18),
                FirstResponseDueAt = now.AddHours(-14),
                FirstRespondedAt = now.AddHours(-16),
                ResolutionDueAt = now.AddHours(6),
                IsSlaBreached = false
            };
            context.ServiceRequests.Add(req1);
            await context.SaveChangesAsync();

            context.RequestStatusHistories.AddRange(
                new RequestStatusHistory { ServiceRequestId = req1.Id, FromStatus = RequestStatus.New, ToStatus = RequestStatus.New, ChangedById = verifiedRequester.Id, Reason = "Created", CreatedAt = req1.CreatedAt },
                new RequestStatusHistory { ServiceRequestId = req1.Id, FromStatus = RequestStatus.New, ToStatus = RequestStatus.Assigned, ChangedById = itLead.Id, Reason = "Assigned to John Carter", CreatedAt = req1.CreatedAt.AddHours(1) },
                new RequestStatusHistory { ServiceRequestId = req1.Id, FromStatus = RequestStatus.Assigned, ToStatus = RequestStatus.InProgress, ChangedById = itAgent.Id, Reason = "Investigating dock firmware", CreatedAt = req1.CreatedAt.AddHours(2) }
            );

            context.AssignmentHistories.Add(new AssignmentHistory
            {
                ServiceRequestId = req1.Id,
                NewAssigneeId = itAgent.Id,
                AssignedById = itLead.Id,
                Reason = "High priority hardware triage.",
                CreatedAt = req1.CreatedAt.AddHours(1)
            });

            context.RequestComments.AddRange(
                new RequestComment { ServiceRequestId = req1.Id, AuthorId = itAgent.Id, IsInternal = true, Body = "Internal Note: Known issue with Thunderbolt DisplayPort driver version 2.4. Preparing patch package.", CreatedAt = req1.CreatedAt.AddHours(3) },
                new RequestComment { ServiceRequestId = req1.Id, AuthorId = itAgent.Id, IsInternal = false, Body = "Hi Alice, could you please confirm the exact model number printed beneath your dock?", CreatedAt = req1.CreatedAt.AddHours(4) },
                new RequestComment { ServiceRequestId = req1.Id, AuthorId = verifiedRequester.Id, IsInternal = false, Body = "Model is Dell WD19S 180W. Thanks!", CreatedAt = req1.CreatedAt.AddHours(5) }
            );

            // Request 2: New Unassigned in Queue
            var req2 = new ServiceRequest
            {
                ReferenceNumber = $"SR-{currentYear}-000102",
                Title = "VPN connection failing with Error 800",
                Description = "Unable to connect to the corporate VPN from home network today. Getting error code 800: unable to resolve gateway address.",
                RequesterId = verifiedRequester.Id,
                CategoryId = netCategory.Id,
                SupportTeamId = itTeam.Id,
                Priority = RequestPriority.Critical,
                Status = RequestStatus.New,
                PreferredContactMethod = PreferredContactMethod.Email,
                CreatedAt = now.AddMinutes(-40),
                FirstResponseDueAt = now.AddMinutes(20),
                ResolutionDueAt = now.AddHours(3),
                IsSlaBreached = false
            };
            context.ServiceRequests.Add(req2);

            // Request 3: Waiting for Requester
            var req3 = new ServiceRequest
            {
                ReferenceNumber = $"SR-{currentYear}-000103",
                Title = "Desk ergonomic chair replacement",
                Description = "The lumbar support on my chair at desk 12-F is cracked and causing back pain during work hours.",
                RequesterId = verifiedRequester.Id,
                CategoryId = facCategory.Id,
                SupportTeamId = facilitiesTeam.Id,
                AssigneeId = facilitiesAgent.Id,
                Priority = RequestPriority.Low,
                Status = RequestStatus.WaitingForRequester,
                PreferredContactMethod = PreferredContactMethod.Portal,
                CreatedAt = now.AddDays(-2),
                FirstResponseDueAt = now.AddDays(-1),
                FirstRespondedAt = now.AddDays(-1).AddHours(4),
                ResolutionDueAt = now.AddDays(3),
                IsSlaBreached = false
            };
            context.ServiceRequests.Add(req3);
            await context.SaveChangesAsync();

            context.RequestComments.Add(new RequestComment
            {
                ServiceRequestId = req3.Id,
                AuthorId = facilitiesAgent.Id,
                IsInternal = false,
                Body = "Waiting for Requester: Could you please attach a photo of the cracked lumbar lever so we can check if replacement parts or a new chair is required?",
                CreatedAt = now.AddHours(-10)
            });

            // Request 4: Resolved (within 7 days, so eligible to reopen)
            var req4 = new ServiceRequest
            {
                ReferenceNumber = $"SR-{currentYear}-000104",
                Title = "Update health insurance beneficiary details",
                Description = "Please assist with updating my dependent coverage documents after marriage.",
                RequesterId = verifiedRequester.Id,
                CategoryId = payCategory.Id,
                SupportTeamId = hrTeam.Id,
                AssigneeId = hrLead.Id,
                Priority = RequestPriority.Medium,
                Status = RequestStatus.Resolved,
                PreferredContactMethod = PreferredContactMethod.Email,
                CreatedAt = now.AddDays(-3),
                FirstResponseDueAt = now.AddDays(-2),
                FirstRespondedAt = now.AddDays(-2).AddHours(2),
                ResolutionDueAt = now.AddDays(-1),
                ResolvedAt = now.AddDays(-1).AddHours(-4),
                ResolutionSummary = "Beneficiary form processed and confirmed with insurers portal.",
                ResolutionCategory = ResolutionCategory.PermanentFix,
                IsSlaBreached = false
            };
            context.ServiceRequests.Add(req4);

            // Request 5: Breached SLA ticket
            var req5 = new ServiceRequest
            {
                ReferenceNumber = $"SR-{currentYear}-000105",
                Title = "HVAC sensor failure in Server Lab A",
                Description = "Room temperature sensor in Server Lab A is registering 32°C. Need urgent ventilation inspection.",
                RequesterId = verifiedRequester.Id,
                CategoryId = facCategory.Id,
                SupportTeamId = facilitiesTeam.Id,
                AssigneeId = facilitiesAgent.Id,
                Priority = RequestPriority.Critical,
                Status = RequestStatus.InProgress,
                PreferredContactMethod = PreferredContactMethod.Phone,
                CreatedAt = now.AddHours(-12),
                FirstResponseDueAt = now.AddHours(-11),
                FirstRespondedAt = now.AddHours(-11),
                ResolutionDueAt = now.AddHours(-8), // Breached 8 hours ago
                IsSlaBreached = true,
                SlaBreachedAt = now.AddHours(-8)
            };
            context.ServiceRequests.Add(req5);

            // Request 6: Closed ticket with complete audit history
            var req6 = new ServiceRequest
            {
                ReferenceNumber = $"SR-{currentYear}-000106",
                Title = "Request for Visual Studio Enterprise license key",
                Description = "Need Visual Studio Enterprise key for upcoming cloud modernization project.",
                RequesterId = verifiedRequester.Id,
                CategoryId = hwCategory.Id,
                SupportTeamId = itTeam.Id,
                AssigneeId = itAgent.Id,
                Priority = RequestPriority.Medium,
                Status = RequestStatus.Closed,
                PreferredContactMethod = PreferredContactMethod.Portal,
                CreatedAt = now.AddDays(-10),
                FirstResponseDueAt = now.AddDays(-9),
                FirstRespondedAt = now.AddDays(-9).AddHours(3),
                ResolutionDueAt = now.AddDays(-7),
                ResolvedAt = now.AddDays(-8),
                ClosedAt = now.AddDays(-1),
                ResolutionSummary = "Subscription assigned via Microsoft 365 Admin Center.",
                ResolutionCategory = ResolutionCategory.ConfigurationChange,
                IsSlaBreached = false
            };
            context.ServiceRequests.Add(req6);

            await context.SaveChangesAsync();
        }
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string displayName,
        string department,
        bool isActive,
        bool emailConfirmed,
        int? supportTeamId)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = displayName,
                Department = department,
                IsActive = isActive,
                EmailConfirmed = emailConfirmed,
                SupportTeamId = supportTeamId,
                CreatedAt = DateTime.UtcNow
            };
            var result = await userManager.CreateAsync(user, defaultPassword);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Failed to seed user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }
        else
        {
            user.DisplayName = displayName;
            user.Department = department;
            user.IsActive = isActive;
            user.EmailConfirmed = emailConfirmed;
            user.SupportTeamId = supportTeamId;
            await userManager.UpdateAsync(user);
        }

        return user;
    }
}
