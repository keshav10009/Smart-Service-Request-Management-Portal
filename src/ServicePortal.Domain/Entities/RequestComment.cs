using System.ComponentModel.DataAnnotations;

namespace ServicePortal.Domain.Entities;

public class RequestComment
{
    public int Id { get; set; }

    public int ServiceRequestId { get; set; }
    public ServiceRequest ServiceRequest { get; set; } = null!;

    [Required]
    public string AuthorId { get; set; } = string.Empty;
    public ApplicationUser Author { get; set; } = null!;

    /// <summary>
    /// If true, only Support Agents, Team Leads, and Admins can see this comment.
    /// Requesters MUST NEVER see internal comments (AC-03).
    /// </summary>
    public bool IsInternal { get; set; }

    [Required]
    [MaxLength(4000)]
    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<RequestAttachment> Attachments { get; set; } = new List<RequestAttachment>();
}
