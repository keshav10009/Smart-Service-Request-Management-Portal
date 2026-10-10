namespace ServicePortal.Application.DTOs;

public class RequesterDashboardDto
{
    public int OpenRequestsCount { get; set; }
    public int WaitingForRequesterCount { get; set; }
    public int ResolvedRequestsCount { get; set; }
    public int ClosedRequestsCount { get; set; }
    public List<ServiceRequestListItemDto> RecentRequests { get; set; } = [];
}

public class AgentDashboardDto
{
    public int MyAssignedOpenCount { get; set; }
    public int UnassignedTeamQueueCount { get; set; }
    public int DueSoonCount { get; set; }
    public int BreachedCount { get; set; }
    public List<ServiceRequestListItemDto> MyActiveRequests { get; set; } = [];
    public List<ServiceRequestListItemDto> TeamQueue { get; set; } = [];
}

public class TeamLeadDashboardDto
{
    public int TotalTeamRequests { get; set; }
    public int UnassignedCount { get; set; }
    public int BreachedCount { get; set; }
    public double SlaComplianceRatePercentage { get; set; }
    public double AverageResolutionDurationHours { get; set; }
    public Dictionary<string, int> StatusDistribution { get; set; } = [];
    public Dictionary<string, int> PriorityDistribution { get; set; } = [];
    public List<AgentWorkloadDto> AgentWorkload { get; set; } = [];
    public List<ServiceRequestListItemDto> CriticalOrBreachedRequests { get; set; } = [];
}

public class AgentWorkloadDto
{
    public string AgentId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public int ActiveCount { get; set; }
    public int ResolvedCount { get; set; }
}

public class AdminDashboardDto
{
    public int TotalUsersCount { get; set; }
    public int ActiveUsersCount { get; set; }
    public int TotalRequestsCount { get; set; }
    public int TotalBreachedRequestsCount { get; set; }
    public Dictionary<string, int> RequestsByCategory { get; set; } = [];
    public Dictionary<string, int> RequestsByTeam { get; set; } = [];
    public List<ServiceRequestListItemDto> RecentSystemActivity { get; set; } = [];
}
