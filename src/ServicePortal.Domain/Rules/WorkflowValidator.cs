using ServicePortal.Domain.Entities;
using ServicePortal.Domain.Enums;

namespace ServicePortal.Domain.Rules;

public class WorkflowResult
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }

    private WorkflowResult(bool success, string? error)
    {
        IsSuccess = success;
        ErrorMessage = error;
    }

    public static WorkflowResult Success() => new(true, null);
    public static WorkflowResult Failure(string error) => new(false, error);
}

public static class WorkflowValidator
{
    public const int MaxReopenDays = 7;

    /// <summary>
    /// Validates whether a status transition is permitted given current state, target state,
    /// actor role, and supplementary parameters (resolution details, comments, reopen date).
    /// </summary>
    public static WorkflowResult ValidateTransition(
        ServiceRequest request,
        RequestStatus targetStatus,
        string actorRole,
        string actorUserId,
        string? transitionComment = null,
        string? resolutionSummary = null,
        ResolutionCategory? resolutionCategory = null,
        string? reopenReason = null,
        DateTime? currentTime = null)
    {
        var now = currentTime ?? DateTime.UtcNow;

        if (request.Status == RequestStatus.Closed)
        {
            return WorkflowResult.Failure("Closed service requests are final and cannot undergo status transitions. Only administrator correction notes are permitted.");
        }

        if (request.Status == targetStatus)
        {
            return WorkflowResult.Failure($"The request is already in status '{request.Status}'.");
        }

        switch (request.Status)
        {
            case RequestStatus.New:
                if (targetStatus != RequestStatus.Assigned)
                {
                    return WorkflowResult.Failure($"A New request can only transition to 'Assigned'. Cannot transition from 'New' to '{targetStatus}'.");
                }
                if (actorRole != AppRoles.Administrator && actorRole != AppRoles.TeamLead)
                {
                    return WorkflowResult.Failure("Only a Team Lead or Administrator can assign a New request.");
                }
                break;

            case RequestStatus.Assigned:
                if (targetStatus == RequestStatus.InProgress)
                {
                    // Agent, Team Lead, or Admin can move to InProgress
                    if (actorRole == AppRoles.Requester)
                    {
                        return WorkflowResult.Failure("Requesters cannot move assigned requests to 'In Progress'.");
                    }
                    if (actorRole == AppRoles.SupportAgent && request.AssigneeId != actorUserId)
                    {
                        return WorkflowResult.Failure("Agents can only transition requests assigned to themselves to 'In Progress'.");
                    }
                }
                else if (targetStatus == RequestStatus.Closed)
                {
                    if (actorRole != AppRoles.Administrator && actorRole != AppRoles.TeamLead)
                    {
                        return WorkflowResult.Failure("Only a Team Lead or Administrator can cancel/close an assigned request.");
                    }
                }
                else
                {
                    return WorkflowResult.Failure($"Cannot transition directly from 'Assigned' to '{targetStatus}'. Allowed target: 'In Progress'.");
                }
                break;

            case RequestStatus.InProgress:
                if (targetStatus == RequestStatus.WaitingForRequester)
                {
                    if (string.IsNullOrWhiteSpace(transitionComment))
                    {
                        return WorkflowResult.Failure("Transition to 'Waiting for Requester' requires a public comment explaining what information is needed.");
                    }
                }
                else if (targetStatus == RequestStatus.Resolved)
                {
                    if (string.IsNullOrWhiteSpace(resolutionSummary))
                    {
                        return WorkflowResult.Failure("Resolving a request requires a non-empty resolution summary.");
                    }
                    if (!resolutionCategory.HasValue)
                    {
                        return WorkflowResult.Failure("Resolving a request requires a valid resolution category.");
                    }
                }
                else if (targetStatus == RequestStatus.Assigned)
                {
                    if (actorRole != AppRoles.Administrator && actorRole != AppRoles.TeamLead)
                    {
                        return WorkflowResult.Failure("Only a Team Lead or Administrator can reassign a request.");
                    }
                }
                else
                {
                    return WorkflowResult.Failure($"Cannot transition directly from 'In Progress' to '{targetStatus}'.");
                }
                break;

            case RequestStatus.WaitingForRequester:
                if (targetStatus == RequestStatus.InProgress)
                {
                    // Can resume work
                }
                else if (targetStatus == RequestStatus.Resolved)
                {
                    if (string.IsNullOrWhiteSpace(resolutionSummary))
                    {
                        return WorkflowResult.Failure("Resolving a request requires a non-empty resolution summary.");
                    }
                    if (!resolutionCategory.HasValue)
                    {
                        return WorkflowResult.Failure("Resolving a request requires a valid resolution category.");
                    }
                }
                else if (targetStatus == RequestStatus.Closed)
                {
                    if (actorRole != AppRoles.Administrator && actorRole != AppRoles.TeamLead)
                    {
                        return WorkflowResult.Failure("Only a Team Lead or Administrator can close a request waiting for requester.");
                    }
                }
                else
                {
                    return WorkflowResult.Failure($"Cannot transition directly from 'Waiting for Requester' to '{targetStatus}'.");
                }
                break;

            case RequestStatus.Resolved:
                if (targetStatus == RequestStatus.Closed)
                {
                    // Requester, Team Lead, Admin, or system background job can close
                }
                else if (targetStatus == RequestStatus.InProgress) // Reopen
                {
                    if (request.ResolvedAt.HasValue)
                    {
                        var daysSinceResolution = (now - request.ResolvedAt.Value).TotalDays;
                        if (daysSinceResolution > MaxReopenDays)
                        {
                            return WorkflowResult.Failure($"A resolved request cannot be reopened after {MaxReopenDays} calendar days (resolved {daysSinceResolution:F1} days ago).");
                        }
                    }
                    if (string.IsNullOrWhiteSpace(reopenReason))
                    {
                        return WorkflowResult.Failure("Reopening a resolved request requires a stated reason.");
                    }
                }
                else
                {
                    return WorkflowResult.Failure($"Cannot transition from 'Resolved' to '{targetStatus}'. Allowed targets: 'Closed' or 'In Progress' (reopen).");
                }
                break;
        }

        return WorkflowResult.Success();
    }
}
