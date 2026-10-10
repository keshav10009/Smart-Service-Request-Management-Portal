using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServicePortal.Application.DTOs;
using ServicePortal.Application.Interfaces;
using ServicePortal.Domain.Entities;
using ServicePortal.Domain.Enums;
using ServicePortal.Infrastructure.BackgroundJobs;
using ServicePortal.Infrastructure.Data;
using Xunit;

namespace ServicePortal.IntegrationTests;

public class AcceptanceCriteriaIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AcceptanceCriteriaIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AC01_VerifiedRequester_SubmitsValidRequest_ReceivesUniqueReferenceNumber()
    {
        using var scope = _factory.Services.CreateScope();
        var requestService = scope.ServiceProvider.GetRequiredService<IServiceRequestService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = await userManager.FindByEmailAsync("requester1@contoso.com");
        Assert.NotNull(user);
        Assert.True(user.EmailConfirmed);

        var category = await context.Categories.FirstAsync();

        var dto = new CreateRequestDto
        {
            Title = "Integration Test Request for AC-01",
            Description = "Testing verified requester submission and unique reference generation.",
            CategoryId = category.Id,
            Priority = RequestPriority.Medium,
            PreferredContactMethod = PreferredContactMethod.Portal
        };

        var (success, error, requestId, refNumber) = await requestService.CreateRequestAsync(dto, user.Id);

        Assert.True(success, error);
        Assert.True(requestId > 0);
        Assert.StartsWith($"SR-{DateTime.UtcNow.Year}-", refNumber);
    }

    [Fact]
    public async Task AC02_IDOR_RequesterCannotAccessAnotherRequestersTicket()
    {
        using var scope = _factory.Services.CreateScope();
        var requestService = scope.ServiceProvider.GetRequiredService<IServiceRequestService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Requester 1
        var requester1 = await userManager.FindByEmailAsync("requester1@contoso.com");
        Assert.NotNull(requester1);

        // Create second distinct requester
        var requester2 = await userManager.FindByEmailAsync("requester2@contoso.com");
        if (requester2 == null)
        {
            requester2 = new ApplicationUser
            {
                UserName = "requester2@contoso.com",
                Email = "requester2@contoso.com",
                DisplayName = "Bob Unauthorized",
                IsActive = true,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(requester2, "Pass@123");
            await userManager.AddToRoleAsync(requester2, AppRoles.Requester);
        }

        // Requester 1 submits ticket
        var category = await context.Categories.FirstAsync();
        var (success, _, ticketId, _) = await requestService.CreateRequestAsync(new CreateRequestDto
        {
            Title = "Private Financial Support Ticket",
            Description = "Sensitive requester 1 payload.",
            CategoryId = category.Id,
            Priority = RequestPriority.Low
        }, requester1.Id);
        Assert.True(success);

        // Requester 2 attempts to query Requester 1's ticket (IDOR attack simulation)
        var idorResult = await requestService.GetByIdAsync(ticketId, requester2.Id, new List<string> { AppRoles.Requester });

        // Access MUST be strictly denied (returns null)
        Assert.Null(idorResult);
    }

    [Fact]
    public async Task AC03_InternalComments_NeverAppearInRequesterViewsOrExports()
    {
        using var scope = _factory.Services.CreateScope();
        var requestService = scope.ServiceProvider.GetRequiredService<IServiceRequestService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var requester = await userManager.FindByEmailAsync("requester1@contoso.com");
        var agent = await userManager.FindByEmailAsync("agent.john@contoso.com");
        var category = await context.Categories.FirstAsync();

        // Create ticket
        var (success, _, ticketId, _) = await requestService.CreateRequestAsync(new CreateRequestDto
        {
            Title = "Laptop Motherboard Issue",
            Description = "Detailed description.",
            CategoryId = category.Id,
            Priority = RequestPriority.High
        }, requester!.Id);
        Assert.True(success);

        // Staff adds internal note
        var internalResult = await requestService.AddCommentAsync(ticketId, new AddCommentDto
        {
            Body = "INTERNAL CONFIDENTIAL: Hardware replacement approved under warranty code #9941.",
            IsInternal = true
        }, agent!.Id, new List<string> { AppRoles.SupportAgent });
        Assert.True(internalResult.IsSuccess);

        // Staff adds public note
        var publicResult = await requestService.AddCommentAsync(ticketId, new AddCommentDto
        {
            Body = "PUBLIC: Hi Alice, we are preparing your replacement laptop.",
            IsInternal = false
        }, agent.Id, new List<string> { AppRoles.SupportAgent });
        Assert.True(publicResult.IsSuccess);

        // Requester views ticket
        var requesterView = await requestService.GetByIdAsync(ticketId, requester.Id, new List<string> { AppRoles.Requester });

        Assert.NotNull(requesterView);
        // Requester view MUST NOT include internal comment
        Assert.DoesNotContain(requesterView.Comments, c => c.IsInternal || c.Body.Contains("INTERNAL CONFIDENTIAL"));
        Assert.Contains(requesterView.Comments, c => c.Body.Contains("PUBLIC"));
    }

    [Fact]
    public async Task AC04_InvalidStatusTransitions_RejectedByServer()
    {
        using var scope = _factory.Services.CreateScope();
        var requestService = scope.ServiceProvider.GetRequiredService<IServiceRequestService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var requester = await userManager.FindByEmailAsync("requester1@contoso.com");
        var category = await context.Categories.FirstAsync();

        // Create new ticket (Status: New)
        var (_, _, ticketId, _) = await requestService.CreateRequestAsync(new CreateRequestDto
        {
            Title = "Transition Validation Ticket",
            Description = "Checking forbidden transitions.",
            CategoryId = category.Id,
            Priority = RequestPriority.Medium
        }, requester!.Id);

        // Illegal attempt: New -> InProgress directly (must be assigned first)
        var invalidTransition = await requestService.TransitionStatusAsync(
            ticketId,
            RequestStatus.InProgress,
            requester.Id,
            new List<string> { AppRoles.Requester },
            new StatusTransitionDto());

        Assert.False(invalidTransition.IsSuccess);
        Assert.Contains("can only transition to 'Assigned'", invalidTransition.ErrorMessage);

        // Illegal attempt: New -> Resolved directly
        var illegalResolve = await requestService.TransitionStatusAsync(
            ticketId,
            RequestStatus.Resolved,
            requester.Id,
            new List<string> { AppRoles.Requester },
            new StatusTransitionDto());

        Assert.False(illegalResolve.IsSuccess);
    }

    [Fact]
    public async Task AC05_TeamLead_CanOnlyAssignToActiveAgentInSameTeam()
    {
        using var scope = _factory.Services.CreateScope();
        var requestService = scope.ServiceProvider.GetRequiredService<IServiceRequestService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var itLead = await userManager.FindByEmailAsync("lead.it@contoso.com");
        var itAgent = await userManager.FindByEmailAsync("agent.john@contoso.com");
        var facAgent = await userManager.FindByEmailAsync("agent.sarah@contoso.com"); // Facilities Team!

        var itCategory = await context.Categories.FirstAsync(c => c.SupportTeam.Name.Contains("IT"));

        // Create IT Ticket
        var (_, _, ticketId, _) = await requestService.CreateRequestAsync(new CreateRequestDto
        {
            Title = "IT Network Outage",
            Description = "Network switch down.",
            CategoryId = itCategory.Id,
            Priority = RequestPriority.Critical
        }, itLead!.Id);

        // Attempt to assign IT ticket to Facilities agent (Cross-team assignment violation)
        var invalidAssign = await requestService.AssignRequestAsync(
            ticketId,
            facAgent!.Id,
            itLead.Id,
            new List<string> { AppRoles.TeamLead });

        Assert.False(invalidAssign.IsSuccess);
        Assert.Contains("does not belong to the request's team", invalidAssign.ErrorMessage);

        // Valid assignment to member of IT team
        var validAssign = await requestService.AssignRequestAsync(
            ticketId,
            itAgent!.Id,
            itLead.Id,
            new List<string> { AppRoles.TeamLead });

        Assert.True(validAssign.IsSuccess);
    }

    [Fact]
    public async Task AC07_BackgroundJob_AutoClosesEligibleResolvedRequests()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var scopeFactory = scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var logger = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AutoCloseResolvedHostedService>>();

        var requester = await userManager.FindByEmailAsync("requester1@contoso.com");
        var category = await context.Categories.FirstAsync();

        // Create a ticket that was resolved 8 days ago (eligible for auto-closure)
        var eligibleRequest = new ServiceRequest
        {
            ReferenceNumber = $"SR-{DateTime.UtcNow.Year}-999001",
            Title = "Auto-Closure Test Ticket",
            Description = "Testing automated 7-day closure.",
            RequesterId = requester!.Id,
            CategoryId = category.Id,
            SupportTeamId = category.SupportTeamId,
            Priority = RequestPriority.Low,
            Status = RequestStatus.Resolved,
            CreatedAt = DateTime.UtcNow.AddDays(-12),
            FirstResponseDueAt = DateTime.UtcNow.AddDays(-10),
            ResolutionDueAt = DateTime.UtcNow.AddDays(-5),
            ResolvedAt = DateTime.UtcNow.AddDays(-8), // 8 days ago (> 7 days)
            ResolutionSummary = "Fix completed"
        };
        context.ServiceRequests.Add(eligibleRequest);
        await context.SaveChangesAsync();

        // Run auto-close service process
        var worker = new AutoCloseResolvedHostedService(scopeFactory, logger);
        var closedCount = await worker.ProcessEligibleClosuresAsync();

        Assert.True(closedCount >= 1);

        // Reload request from db to verify closed status
        var updated = await context.ServiceRequests
            .Include(r => r.StatusHistories)
            .AsNoTracking()
            .FirstAsync(r => r.Id == eligibleRequest.Id);

        Assert.Equal(RequestStatus.Closed, updated.Status);
        Assert.NotNull(updated.ClosedAt);
        Assert.Contains(updated.StatusHistories, h => h.Reason != null && h.Reason.Contains("Automatically closed by background job"));
    }

    [Fact]
    public async Task AC10_ConcurrentUpdates_ProduceConflictAndDoNotSilentlyOverwrite()
    {
        using var scope = _factory.Services.CreateScope();
        var requestService = scope.ServiceProvider.GetRequiredService<IServiceRequestService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var itLead = await userManager.FindByEmailAsync("lead.it@contoso.com");
        var itAgent = await userManager.FindByEmailAsync("agent.john@contoso.com");
        var category = await context.Categories.FirstAsync(c => c.SupportTeam.Name.Contains("IT"));

        // Create ticket
        var (_, _, ticketId, _) = await requestService.CreateRequestAsync(new CreateRequestDto
        {
            Title = "Concurrency Conflict Test",
            Description = "Testing optimistic locking token.",
            CategoryId = category.Id,
            Priority = RequestPriority.Medium
        }, itLead!.Id);

        // Assign to Agent
        await requestService.AssignRequestAsync(ticketId, itAgent!.Id, itLead.Id, new List<string> { AppRoles.TeamLead });

        // Retrieve current ticket and capture RowVersion
        var snapshot = await requestService.GetByIdAsync(ticketId, itLead.Id, new List<string> { AppRoles.TeamLead });
        Assert.NotNull(snapshot);
        var originalRowVersion = snapshot.RowVersion;

        // User A performs valid transition to InProgress (this updates RowVersion in DB)
        var transitionA = await requestService.TransitionStatusAsync(
            ticketId,
            RequestStatus.InProgress,
            itAgent.Id,
            new List<string> { AppRoles.SupportAgent },
            new StatusTransitionDto { RowVersion = originalRowVersion });
        Assert.True(transitionA.IsSuccess);

        // User B attempts another transition using the STALE originalRowVersion
        var transitionB = await requestService.TransitionStatusAsync(
            ticketId,
            RequestStatus.WaitingForRequester,
            itAgent.Id,
            new List<string> { AppRoles.SupportAgent },
            new StatusTransitionDto
            {
                RowVersion = originalRowVersion, // Stale!
                Comment = "What is your OS?"
            });

        // User B's change MUST be rejected due to concurrency token mismatch
        Assert.False(transitionB.IsSuccess);
        Assert.Contains("concurrently", transitionB.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
