using ServicePortal.Domain.Entities;
using ServicePortal.Domain.Enums;
using ServicePortal.Domain.Rules;
using Xunit;

namespace ServicePortal.UnitTests.Workflow;

public class WorkflowValidatorTests
{
    private readonly string _leadId = "lead-1";
    private readonly string _agentId = "agent-1";
    private readonly string _otherAgentId = "agent-2";
    private readonly string _requesterId = "req-1";

    [Fact]
    public void ValidateTransition_NewToAssigned_ByTeamLead_Succeeds()
    {
        var request = CreateTestRequest(RequestStatus.New);

        var result = WorkflowValidator.ValidateTransition(
            request,
            RequestStatus.Assigned,
            AppRoles.TeamLead,
            _leadId);

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void ValidateTransition_NewToAssigned_ByRequester_Fails()
    {
        var request = CreateTestRequest(RequestStatus.New);

        var result = WorkflowValidator.ValidateTransition(
            request,
            RequestStatus.Assigned,
            AppRoles.Requester,
            _requesterId);

        Assert.False(result.IsSuccess);
        Assert.Contains("Only a Team Lead or Administrator can assign", result.ErrorMessage);
    }

    [Fact]
    public void ValidateTransition_NewToInProgressDirectly_Fails()
    {
        var request = CreateTestRequest(RequestStatus.New);

        var result = WorkflowValidator.ValidateTransition(
            request,
            RequestStatus.InProgress,
            AppRoles.TeamLead,
            _leadId);

        Assert.False(result.IsSuccess);
        Assert.Contains("A New request can only transition to 'Assigned'", result.ErrorMessage);
    }

    [Fact]
    public void ValidateTransition_AssignedToInProgress_ByAssignedAgent_Succeeds()
    {
        var request = CreateTestRequest(RequestStatus.Assigned, assigneeId: _agentId);

        var result = WorkflowValidator.ValidateTransition(
            request,
            RequestStatus.InProgress,
            AppRoles.SupportAgent,
            _agentId);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidateTransition_AssignedToInProgress_ByOtherAgent_Fails()
    {
        var request = CreateTestRequest(RequestStatus.Assigned, assigneeId: _agentId);

        var result = WorkflowValidator.ValidateTransition(
            request,
            RequestStatus.InProgress,
            AppRoles.SupportAgent,
            _otherAgentId);

        Assert.False(result.IsSuccess);
        Assert.Contains("Agents can only transition requests assigned to themselves", result.ErrorMessage);
    }

    [Fact]
    public void ValidateTransition_InProgressToWaitingForRequester_WithoutComment_Fails()
    {
        var request = CreateTestRequest(RequestStatus.InProgress, assigneeId: _agentId);

        var result = WorkflowValidator.ValidateTransition(
            request,
            RequestStatus.WaitingForRequester,
            AppRoles.SupportAgent,
            _agentId,
            transitionComment: null);

        Assert.False(result.IsSuccess);
        Assert.Contains("requires a public comment", result.ErrorMessage);
    }

    [Fact]
    public void ValidateTransition_InProgressToWaitingForRequester_WithComment_Succeeds()
    {
        var request = CreateTestRequest(RequestStatus.InProgress, assigneeId: _agentId);

        var result = WorkflowValidator.ValidateTransition(
            request,
            RequestStatus.WaitingForRequester,
            AppRoles.SupportAgent,
            _agentId,
            transitionComment: "Please provide serial number of the unit.");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidateTransition_InProgressToResolved_WithoutSummary_Fails()
    {
        var request = CreateTestRequest(RequestStatus.InProgress, assigneeId: _agentId);

        var result = WorkflowValidator.ValidateTransition(
            request,
            RequestStatus.Resolved,
            AppRoles.SupportAgent,
            _agentId,
            resolutionSummary: null,
            resolutionCategory: ResolutionCategory.PermanentFix);

        Assert.False(result.IsSuccess);
        Assert.Contains("non-empty resolution summary", result.ErrorMessage);
    }

    [Fact]
    public void ValidateTransition_InProgressToResolved_WithValidSummaryAndCategory_Succeeds()
    {
        var request = CreateTestRequest(RequestStatus.InProgress, assigneeId: _agentId);

        var result = WorkflowValidator.ValidateTransition(
            request,
            RequestStatus.Resolved,
            AppRoles.SupportAgent,
            _agentId,
            resolutionSummary: "Installed firmware update v1.4 to resolve dock disconnect.",
            resolutionCategory: ResolutionCategory.PermanentFix);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidateTransition_ResolvedToReopened_WithinSevenDays_WithReason_Succeeds()
    {
        var now = DateTime.UtcNow;
        var request = CreateTestRequest(RequestStatus.Resolved, assigneeId: _agentId);
        request.ResolvedAt = now.AddDays(-3); // 3 days ago

        var result = WorkflowValidator.ValidateTransition(
            request,
            RequestStatus.InProgress,
            AppRoles.Requester,
            _requesterId,
            reopenReason: "Flickering issue reoccurred today after restarting laptop.",
            currentTime: now);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidateTransition_ResolvedToReopened_AfterSevenDays_StrictlyFails()
    {
        var now = DateTime.UtcNow;
        var request = CreateTestRequest(RequestStatus.Resolved, assigneeId: _agentId);
        request.ResolvedAt = now.AddDays(-8); // 8 days ago (> 7 days)

        var result = WorkflowValidator.ValidateTransition(
            request,
            RequestStatus.InProgress,
            AppRoles.Requester,
            _requesterId,
            reopenReason: "Still broken",
            currentTime: now);

        Assert.False(result.IsSuccess);
        Assert.Contains("cannot be reopened after 7 calendar days", result.ErrorMessage);
    }

    [Fact]
    public void ValidateTransition_ClosedRequest_CannotBeTransitioned()
    {
        var request = CreateTestRequest(RequestStatus.Closed);

        var result = WorkflowValidator.ValidateTransition(
            request,
            RequestStatus.InProgress,
            AppRoles.Administrator,
            "admin-1");

        Assert.False(result.IsSuccess);
        Assert.Contains("Closed service requests are final", result.ErrorMessage);
    }

    private ServiceRequest CreateTestRequest(RequestStatus status, string? assigneeId = null)
    {
        return new ServiceRequest
        {
            Id = 1,
            ReferenceNumber = "SR-2026-000101",
            Title = "Test Request",
            Description = "Test request description for unit testing.",
            RequesterId = _requesterId,
            CategoryId = 1,
            SupportTeamId = 1,
            AssigneeId = assigneeId,
            Priority = RequestPriority.Medium,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };
    }
}
