namespace ServicePortal.Domain.Entities;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SupportTeamId { get; set; }
    public SupportTeam SupportTeam { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ServiceRequest> Requests { get; set; } = new List<ServiceRequest>();
}
