using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServicePortal.Application.Interfaces;
using ServicePortal.Domain.Entities;
using ServicePortal.Domain.Enums;
using ServicePortal.Infrastructure.Data;

namespace ServicePortal.Infrastructure.BackgroundJobs;

public class SlaBreachHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SlaBreachHostedService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(60);

    public SlaBreachHostedService(IServiceScopeFactory scopeFactory, ILogger<SlaBreachHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SlaBreachHostedService background worker initialized.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DetectSlaBreachesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during background SLA breach detection execution.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    public async Task<int> DetectSlaBreachesAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var auditLog = scope.ServiceProvider.GetRequiredService<IAuditLogService>();

        var now = DateTime.UtcNow;

        var breachedRequests = await context.ServiceRequests
            .Include(r => r.SupportTeam)
            .Include(r => r.Assignee)
            .Where(r => !r.IsSlaBreached &&
                        r.Status != RequestStatus.Resolved &&
                        r.Status != RequestStatus.Closed &&
                        r.ResolutionDueAt < now)
            .ToListAsync(cancellationToken);

        if (breachedRequests.Count == 0)
        {
            return 0;
        }

        foreach (var request in breachedRequests)
        {
            request.IsSlaBreached = true;
            request.SlaBreachedAt = now;

            _logger.LogWarning("Detected SLA breach on ServiceRequest {Ref} (Due: {Due}, Now: {Now})",
                request.ReferenceNumber, request.ResolutionDueAt, now);

            // Notify Assignee if assigned
            if (!string.IsNullOrEmpty(request.AssigneeId))
            {
                await notificationService.CreateNotificationAsync(
                    request.AssigneeId,
                    NotificationType.SlaBreach,
                    $"SLA BREACH: {request.ReferenceNumber}",
                    $"Service request '{request.Title}' has breached its SLA resolution deadline ({request.ResolutionDueAt:g}).",
                    request.Id,
                    cancellationToken);
            }

            // Notify Team Lead(s)
            var teamLeads = await context.Users
                .Where(u => u.SupportTeamId == request.SupportTeamId && u.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var lead in teamLeads)
            {
                await notificationService.CreateNotificationAsync(
                    lead.Id,
                    NotificationType.SlaBreach,
                    $"SLA BREACH ALERT: {request.ReferenceNumber}",
                    $"Request '{request.Title}' in team '{request.SupportTeam.Name}' has breached its resolution target.",
                    request.Id,
                    cancellationToken);
            }

            // Record Audit Log (AC-06)
            await auditLog.LogAsync(
                null,
                "SlaMonitorService",
                "SlaBreachDetected",
                nameof(ServiceRequest),
                request.Id.ToString(),
                $"SLA resolution target of {request.ResolutionDueAt:u} was breached.",
                null,
                null,
                cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        return breachedRequests.Count;
    }
}
