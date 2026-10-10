using System.ComponentModel.DataAnnotations;
using ServicePortal.Domain.Enums;

namespace ServicePortal.Domain.Entities;

public class ServiceRequest
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string ReferenceNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public string RequesterId { get; set; } = string.Empty;
    public ApplicationUser Requester { get; set; } = null!;

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public int SupportTeamId { get; set; }
    public SupportTeam SupportTeam { get; set; } = null!;

    public string? AssigneeId { get; set; }
    public ApplicationUser? Assignee { get; set; }

    public RequestPriority Priority { get; set; } = RequestPriority.Medium;
    public RequestStatus Status { get; set; } = RequestStatus.New;
    public PreferredContactMethod PreferredContactMethod { get; set; } = PreferredContactMethod.Portal;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public DateTime FirstResponseDueAt { get; set; }
    public DateTime? FirstRespondedAt { get; set; }

    public DateTime ResolutionDueAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public bool IsSlaBreached { get; set; }
    public DateTime? SlaBreachedAt { get; set; }

    [MaxLength(2000)]
    public string? ResolutionSummary { get; set; }
    public ResolutionCategory? ResolutionCategory { get; set; }

    [MaxLength(1000)]
    public string? ReopenReason { get; set; }
    public DateTime? ReopenedAt { get; set; }

    [MaxLength(2000)]
    public string? CorrectionNotes { get; set; }

    [ConcurrencyCheck]
    public byte[] RowVersion { get; set; } = Guid.NewGuid().ToByteArray();

    public ICollection<RequestComment> Comments { get; set; } = new List<RequestComment>();
    public ICollection<RequestAttachment> Attachments { get; set; } = new List<RequestAttachment>();
    public ICollection<RequestStatusHistory> StatusHistories { get; set; } = new List<RequestStatusHistory>();
    public ICollection<AssignmentHistory> AssignmentHistories { get; set; } = new List<AssignmentHistory>();
}
