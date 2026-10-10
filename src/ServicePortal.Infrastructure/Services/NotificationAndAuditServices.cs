using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServicePortal.Application.Interfaces;
using ServicePortal.Domain.Entities;
using ServicePortal.Domain.Enums;
using ServicePortal.Infrastructure.Data;

namespace ServicePortal.Infrastructure.Services;

public class EmailSender : IEmailSender
{
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(ILogger<EmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(string toEmail, string subject, string htmlMessage, CancellationToken cancellationToken = default)
    {
        try
        {
            // Simulated / Mocked enterprise notification sender that logs safely without exposing sensitive secrets
            _logger.LogInformation("[EmailSender] Dispatching simulated email to: {ToEmail} | Subject: '{Subject}'", toEmail, subject);
        }
        catch (Exception ex)
        {
            // Notification failures must NOT crash or roll back the primary business transaction (AC requirement)
            _logger.LogError(ex, "[EmailSender] Failed to dispatch email to {ToEmail}. Non-blocking failure.", toEmail);
        }

        return Task.CompletedTask;
    }
}

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ApplicationDbContext context, ILogger<NotificationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task CreateNotificationAsync(
        string recipientId,
        NotificationType type,
        string title,
        string message,
        int? relatedRequestId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var notification = new Notification
            {
                RecipientId = recipientId,
                Type = type,
                Title = title,
                Message = message,
                RelatedRequestId = relatedRequestId,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Created in-app notification for User: {UserId} of Type: {Type}", recipientId, type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist in-app notification for user {UserId}. Non-blocking failure.", recipientId);
        }
    }

    public async Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.RecipientId == userId && !n.IsRead, cancellationToken);
    }

    public async Task MarkAsReadAsync(int notificationId, string userId, CancellationToken cancellationToken = default)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientId == userId, cancellationToken);

        if (notification != null)
        {
            notification.IsRead = true;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default)
    {
        var unread = await _context.Notifications
            .Where(n => n.RecipientId == userId && !n.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var item in unread)
        {
            item.IsRead = true;
        }

        if (unread.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

public class AuditLogService : IAuditLogService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(ApplicationDbContext context, ILogger<AuditLogService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogAsync(
        string? actorId,
        string actorName,
        string action,
        string entityName,
        string entityId,
        string details,
        string? correlationId = null,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var log = new AuditLog
            {
                ActorId = actorId,
                ActorName = actorName,
                Action = action,
                EntityName = entityName,
                EntityId = entityId,
                Details = details,
                CorrelationId = correlationId,
                IpAddress = ipAddress,
                Timestamp = DateTime.UtcNow
            };

            _context.AuditLogs.Add(log);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("AuditLog: [{Action}] on {EntityName}:{EntityId} by {ActorName} (CorrelationId: {CorrelationId})",
                action, entityName, entityId, actorName, correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit log entry. Non-blocking failure.");
        }
    }
}
