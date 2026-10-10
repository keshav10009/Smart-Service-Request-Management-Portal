using ServicePortal.Domain.Enums;

namespace ServicePortal.Domain.Entities;

public class SlaPolicy
{
    public int Id { get; set; }
    public RequestPriority Priority { get; set; }
    public int ResponseTargetHours { get; set; }
    public int ResolutionTargetHours { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
