using Microsoft.AspNetCore.Identity;

namespace ServicePortal.Domain.Entities;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public string? Department { get; set; }
    public bool IsActive { get; set; } = true;
    public int? SupportTeamId { get; set; }
    public SupportTeam? SupportTeam { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ServiceRequest> RequestedServices { get; set; } = new List<ServiceRequest>();
    public ICollection<ServiceRequest> AssignedServices { get; set; } = new List<ServiceRequest>();
    public ICollection<RequestComment> Comments { get; set; } = new List<RequestComment>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
