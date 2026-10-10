using System.ComponentModel.DataAnnotations;

namespace ServicePortal.Domain.Entities;

public class RequestAttachment
{
    public int Id { get; set; }

    public int ServiceRequestId { get; set; }
    public ServiceRequest ServiceRequest { get; set; } = null!;

    public int? RequestCommentId { get; set; }
    public RequestComment? RequestComment { get; set; }

    [Required]
    [MaxLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string StoredFileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    [Required]
    public string UploaderId { get; set; } = string.Empty;
    public ApplicationUser Uploader { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
