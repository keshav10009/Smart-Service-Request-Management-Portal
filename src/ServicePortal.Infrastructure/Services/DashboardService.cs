using Microsoft.EntityFrameworkCore;
using ServicePortal.Application.DTOs;
using ServicePortal.Application.Interfaces;
using ServicePortal.Domain.Entities;
using ServicePortal.Domain.Enums;
using ServicePortal.Infrastructure.Data;

namespace ServicePortal.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _context;
    private readonly ISlaCalculator _slaCalculator;

    public DashboardService(ApplicationDbContext context, ISlaCalculator slaCalculator)
    {
        _context = context;
        _slaCalculator = slaCalculator;
    }

    public async Task<RequesterDashboardDto> GetRequesterDashboardAsync(string userId, CancellationToken cancellationToken = default)
    {
        var requests = await _context.ServiceRequests
            .Include(r => r.Category)
            .Include(r => r.SupportTeam)
            .Include(r => r.Assignee)
            .Include(r => r.Requester)
            .Where(r => r.RequesterId == userId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var openCount = requests.Count(r => r.Status == RequestStatus.New || r.Status == RequestStatus.Assigned || r.Status == RequestStatus.InProgress);
        var waitingCount = requests.Count(r => r.Status == RequestStatus.WaitingForRequester);
        var resolvedCount = requests.Count(r => r.Status == RequestStatus.Resolved);
        var closedCount = requests.Count(r => r.Status == RequestStatus.Closed);

        var recent = requests
            .OrderByDescending(r => r.CreatedAt)
            .Take(5)
            .Select(r => MapToListItem(r))
            .ToList();

        return new RequesterDashboardDto
        {
            OpenRequestsCount = openCount,
            WaitingForRequesterCount = waitingCount,
            ResolvedRequestsCount = resolvedCount,
            ClosedRequestsCount = closedCount,
            RecentRequests = recent
        };
    }

    public async Task<AgentDashboardDto> GetAgentDashboardAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
        var teamId = user?.SupportTeamId;

        var myRequests = await _context.ServiceRequests
            .Include(r => r.Category)
            .Include(r => r.SupportTeam)
            .Include(r => r.Requester)
            .Include(r => r.Assignee)
            .Where(r => r.AssigneeId == userId && r.Status != RequestStatus.Closed)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var teamQueue = await _context.ServiceRequests
            .Include(r => r.Category)
            .Include(r => r.SupportTeam)
            .Include(r => r.Requester)
            .Include(r => r.Assignee)
            .Where(r => r.SupportTeamId == teamId && r.AssigneeId == null && r.Status != RequestStatus.Closed)
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.ResolutionDueAt)
            .Take(10)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var dueSoonCount = myRequests.Count(r => r.Status != RequestStatus.Resolved &&
                                                 r.ResolutionDueAt > now &&
                                                 r.ResolutionDueAt <= now.AddHours(2));

        var breachedCount = myRequests.Count(r => r.IsSlaBreached ||
                                                  (r.Status != RequestStatus.Resolved && r.ResolutionDueAt < now));

        return new AgentDashboardDto
        {
            MyAssignedOpenCount = myRequests.Count(r => r.Status != RequestStatus.Resolved),
            UnassignedTeamQueueCount = teamQueue.Count,
            DueSoonCount = dueSoonCount,
            BreachedCount = breachedCount,
            MyActiveRequests = myRequests.OrderBy(r => r.ResolutionDueAt).Take(8).Select(r => MapToListItem(r)).ToList(),
            TeamQueue = teamQueue.Select(r => MapToListItem(r)).ToList()
        };
    }

    public async Task<TeamLeadDashboardDto> GetTeamLeadDashboardAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
        var teamId = user?.SupportTeamId;

        var teamRequests = await _context.ServiceRequests
            .Include(r => r.Category)
            .Include(r => r.SupportTeam)
            .Include(r => r.Requester)
            .Include(r => r.Assignee)
            .Where(r => r.SupportTeamId == teamId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var total = teamRequests.Count;
        var unassigned = teamRequests.Count(r => r.AssigneeId == null && r.Status != RequestStatus.Closed);
        var breached = teamRequests.Count(r => r.IsSlaBreached || (r.Status != RequestStatus.Resolved && r.Status != RequestStatus.Closed && r.ResolutionDueAt < now));

        // SLA compliance rate
        var resolvedOrClosed = teamRequests.Where(r => r.ResolvedAt.HasValue).ToList();
        var compliantCount = resolvedOrClosed.Count(r => r.ResolvedAt <= r.ResolutionDueAt);
        var complianceRate = resolvedOrClosed.Count > 0 ? (compliantCount * 100.0 / resolvedOrClosed.Count) : 100.0;

        // Average resolution duration
        var durations = resolvedOrClosed
            .Select(r => (r.ResolvedAt!.Value - r.CreatedAt).TotalHours)
            .ToList();
        var avgDuration = durations.Count > 0 ? durations.Average() : 0.0;

        // Distributions
        var statusDist = teamRequests
            .GroupBy(r => r.Status.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var priorityDist = teamRequests
            .GroupBy(r => r.Priority.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        // Agent workload in team
        var teamAgents = await _context.Users
            .Where(u => u.SupportTeamId == teamId && u.IsActive)
            .ToListAsync(cancellationToken);

        var workload = teamAgents.Select(agent => new AgentWorkloadDto
        {
            AgentId = agent.Id,
            AgentName = agent.DisplayName,
            ActiveCount = teamRequests.Count(r => r.AssigneeId == agent.Id && r.Status != RequestStatus.Resolved && r.Status != RequestStatus.Closed),
            ResolvedCount = teamRequests.Count(r => r.AssigneeId == agent.Id && (r.Status == RequestStatus.Resolved || r.Status == RequestStatus.Closed))
        }).ToList();

        var criticalOrBreached = teamRequests
            .Where(r => (r.Priority == RequestPriority.Critical || r.IsSlaBreached) && r.Status != RequestStatus.Closed)
            .OrderByDescending(r => r.Priority)
            .Take(8)
            .Select(r => MapToListItem(r))
            .ToList();

        return new TeamLeadDashboardDto
        {
            TotalTeamRequests = total,
            UnassignedCount = unassigned,
            BreachedCount = breached,
            SlaComplianceRatePercentage = Math.Round(complianceRate, 1),
            AverageResolutionDurationHours = Math.Round(avgDuration, 1),
            StatusDistribution = statusDist,
            PriorityDistribution = priorityDist,
            AgentWorkload = workload,
            CriticalOrBreachedRequests = criticalOrBreached
        };
    }

    public async Task<AdminDashboardDto> GetAdminDashboardAsync(CancellationToken cancellationToken = default)
    {
        var totalUsers = await _context.Users.CountAsync(cancellationToken);
        var activeUsers = await _context.Users.CountAsync(u => u.IsActive, cancellationToken);
        var totalRequests = await _context.ServiceRequests.CountAsync(cancellationToken);
        var totalBreached = await _context.ServiceRequests.CountAsync(r => r.IsSlaBreached, cancellationToken);

        var byCategory = await _context.ServiceRequests
            .GroupBy(r => r.Category.Name)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var byTeam = await _context.ServiceRequests
            .GroupBy(r => r.SupportTeam.Name)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var recent = await _context.ServiceRequests
            .Include(r => r.Category)
            .Include(r => r.SupportTeam)
            .Include(r => r.Requester)
            .Include(r => r.Assignee)
            .OrderByDescending(r => r.CreatedAt)
            .Take(8)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new AdminDashboardDto
        {
            TotalUsersCount = totalUsers,
            ActiveUsersCount = activeUsers,
            TotalRequestsCount = totalRequests,
            TotalBreachedRequestsCount = totalBreached,
            RequestsByCategory = byCategory,
            RequestsByTeam = byTeam,
            RecentSystemActivity = recent.Select(r => MapToListItem(r)).ToList()
        };
    }

    private ServiceRequestListItemDto MapToListItem(ServiceRequest r)
    {
        var isResolved = r.Status == RequestStatus.Resolved || r.Status == RequestStatus.Closed;
        return new ServiceRequestListItemDto
        {
            Id = r.Id,
            ReferenceNumber = r.ReferenceNumber,
            Title = r.Title,
            CategoryName = r.Category?.Name ?? string.Empty,
            SupportTeamName = r.SupportTeam?.Name ?? string.Empty,
            RequesterName = r.Requester?.DisplayName ?? string.Empty,
            AssigneeName = r.Assignee?.DisplayName,
            Priority = r.Priority,
            Status = r.Status,
            CreatedAt = r.CreatedAt,
            ResolutionDueAt = r.ResolutionDueAt,
            IsSlaBreached = r.IsSlaBreached,
            SlaHealth = _slaCalculator.GetHealthStatus(r.ResolutionDueAt, isResolved, r.ResolvedAt),
            CommentCount = r.Comments?.Count ?? 0,
            AttachmentCount = r.Attachments?.Count ?? 0
        };
    }
}
