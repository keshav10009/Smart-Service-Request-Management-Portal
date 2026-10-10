using ServicePortal.Domain.Enums;

namespace ServicePortal.Application.Interfaces;

public interface IEmailSender
{
    Task SendEmailAsync(string toEmail, string subject, string htmlMessage, CancellationToken cancellationToken = default);
}

public interface INotificationService
{
    Task CreateNotificationAsync(
        string recipientId,
        NotificationType type,
        string title,
        string message,
        int? relatedRequestId = null,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(int notificationId, string userId, CancellationToken cancellationToken = default);

    Task MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default);
}

public interface IAuditLogService
{
    Task LogAsync(
        string? actorId,
        string actorName,
        string action,
        string entityName,
        string entityId,
        string details,
        string? correlationId = null,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);
}
