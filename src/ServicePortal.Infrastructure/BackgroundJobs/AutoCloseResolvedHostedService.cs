using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServicePortal.Application.Interfaces;
using ServicePortal.Domain.Entities;
using ServicePortal.Domain.Enums;
using ServicePortal.Infrastructure.Data;

namespace ServicePortal.Infrastructure.BackgroundJobs;

public class AutoCloseResolvedHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutoCloseResolvedHostedService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(10);
    public const int AutoCloseDays = 7;

    public AutoCloseResolvedHostedService(IServiceScopeFactory scopeFactory, ILogger<AutoCloseResolvedHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AutoCloseResolvedHostedService background worker initialized.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessEligibleClosuresAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during background auto-close of resolved requests.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    public async Task<int> ProcessEligibleClosuresAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var auditLog = scope.ServiceProvider.GetRequiredService<IAuditLogService>();

        var cutoff = DateTime.UtcNow.AddDays(-AutoCloseDays);

        var eligibleRequests = await context.ServiceRequests
            .Include(r => r.Requester)
            .Where(r => r.Status == RequestStatus.Resolved &&
                        r.ResolvedAt.HasValue &&
                        r.ResolvedAt.Value <= cutoff)
            .ToListAsync(cancellationToken);

        if (eligibleRequests.Count == 0)
        {
            return 0;
        }

        var now = DateTime.UtcNow;

        foreach (var request in eligibleRequests)
        {
            request.Status = RequestStatus.Closed;
            request.ClosedAt = now;
            request.UpdatedAt = now;

            _logger.LogInformation("Auto-closing ServiceRequest {Ref} resolved on {ResolvedAt}",
                request.ReferenceNumber, request.ResolvedAt);

            // Record automated status history entry (AC-07)
            var history = new RequestStatusHistory
            {
                ServiceRequestId = request.Id,
                FromStatus = RequestStatus.Resolved,
                ToStatus = RequestStatus.Closed,
                ChangedById = request.RequesterId, // Associate or flag as system
                Reason = $"Automatically closed by background job after {AutoCloseDays} calendar days in Resolved status.",
                CreatedAt = now
            };
            context.RequestStatusHistories.Add(history);

            // Notify requester of automatic closure
            await notificationService.CreateNotificationAsync(
                request.RequesterId,
                NotificationType.StatusChange,
                $"Request Closed: {request.ReferenceNumber}",
                $"Service request '{request.Title}' has been automatically closed following the 7-day post-resolution window.",
                request.Id,
                cancellationToken);

            // Audit Log
            await auditLog.LogAsync(
                null,
                "SystemAutoClosureWorker",
                "AutoCloseRequest",
                nameof(ServiceRequest),
                request.Id.ToString(),
                $"Request automatically closed after {AutoCloseDays} days.",
                null,
                null,
                cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        return eligibleRequests.Count;
    }
}
