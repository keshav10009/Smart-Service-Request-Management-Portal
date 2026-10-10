using System.ComponentModel.DataAnnotations;

namespace ServicePortal.Domain.Entities;

public class AuditLog
{
    public int Id { get; set; }

    public string? ActorId { get; set; }

    [Required]
    [MaxLength(150)]
    public string ActorName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Action { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string EntityName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string EntityId { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [Required]
    public string Details { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? CorrelationId { get; set; }

    [MaxLength(50)]
    public string? IpAddress { get; set; }
}
