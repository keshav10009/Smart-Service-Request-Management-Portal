using System.ComponentModel.DataAnnotations;
using ServicePortal.Domain.Enums;
using ServicePortal.Application.Interfaces;

namespace ServicePortal.Application.DTOs;

public class CreateRequestDto
{
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(200, MinimumLength = 5, ErrorMessage = "Title must be between 5 and 200 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required.")]
    [MinLength(10, ErrorMessage = "Description must be at least 10 characters.")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Category is required.")]
    public int CategoryId { get; set; }

    [Required]
    public RequestPriority Priority { get; set; } = RequestPriority.Medium;

    [Required]
    public PreferredContactMethod PreferredContactMethod { get; set; } = PreferredContactMethod.Portal;

    public bool AcknowledgeDuplicateWarning { get; set; }
}

public class ServiceRequestListItemDto
{
    public int Id { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string SupportTeamName { get; set; } = string.Empty;
    public string RequesterName { get; set; } = string.Empty;
    public string? AssigneeName { get; set; }
    public RequestPriority Priority { get; set; }
    public RequestStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ResolutionDueAt { get; set; }
    public bool IsSlaBreached { get; set; }
    public SlaHealthStatus SlaHealth { get; set; }
    public int CommentCount { get; set; }
    public int AttachmentCount { get; set; }
}

public class ServiceRequestDto
{
    public int Id { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RequesterId { get; set; } = string.Empty;
    public string RequesterName { get; set; } = string.Empty;
    public string RequesterEmail { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int SupportTeamId { get; set; }
    public string SupportTeamName { get; set; } = string.Empty;
    public string? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public RequestPriority Priority { get; set; }
    public RequestStatus Status { get; set; }
    public PreferredContactMethod PreferredContactMethod { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime FirstResponseDueAt { get; set; }
    public DateTime? FirstRespondedAt { get; set; }
    public DateTime ResolutionDueAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public bool IsSlaBreached { get; set; }
    public DateTime? SlaBreachedAt { get; set; }
    public SlaHealthStatus SlaHealth { get; set; }
    public string? ResolutionSummary { get; set; }
    public ResolutionCategory? ResolutionCategory { get; set; }
    public string? ReopenReason { get; set; }
    public DateTime? ReopenedAt { get; set; }
    public string? CorrectionNotes { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public List<CommentDto> Comments { get; set; } = [];
    public List<AttachmentDto> Attachments { get; set; } = [];
    public List<TimelineEventDto> Timeline { get; set; } = [];
}

public class CommentDto
{
    public int Id { get; set; }
    public string AuthorId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AttachmentDto
{
    public int Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string UploaderName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class TimelineEventDto
{
    public string EventType { get; set; } = string.Empty; // "StatusChange", "Comment", "Assignment", "Attachment", "SlaBreach"
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string BadgeClass { get; set; } = "bg-primary";
    public bool IsInternal { get; set; }
}

public class StatusTransitionDto
{
    public RequestStatus TargetStatus { get; set; }
    public string? Comment { get; set; }
    public string? ResolutionSummary { get; set; }
    public ResolutionCategory? ResolutionCategory { get; set; }
    public string? ReopenReason { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public class AddCommentDto
{
    [Required(ErrorMessage = "Comment cannot be empty.")]
    [StringLength(4000, MinimumLength = 2)]
    public string Body { get; set; } = string.Empty;

    public bool IsInternal { get; set; }
}

public class DuplicateCheckResult
{
    public bool HasDuplicate { get; set; }
    public string? ExistingReference { get; set; }
    public string? ExistingTitle { get; set; }
    public DateTime? ExistingCreatedAt { get; set; }
}
