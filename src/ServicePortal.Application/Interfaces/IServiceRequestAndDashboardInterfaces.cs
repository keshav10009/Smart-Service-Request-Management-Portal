using ServicePortal.Application.DTOs;
using ServicePortal.Domain.Enums;
using ServicePortal.Domain.Rules;

namespace ServicePortal.Application.Interfaces;

public class AssignmentResult
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }

    private AssignmentResult(bool success, string? error)
    {
        IsSuccess = success;
        ErrorMessage = error;
    }

    public static AssignmentResult Success() => new(true, null);
    public static AssignmentResult Failure(string error) => new(false, error);
}

public class CommentResult
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }
    public int? CommentId { get; }

    private CommentResult(bool success, string? error, int? id = null)
    {
        IsSuccess = success;
        ErrorMessage = error;
        CommentId = id;
    }

    public static CommentResult Success(int id) => new(true, null, id);
    public static CommentResult Failure(string error) => new(false, error);
}

public interface IServiceRequestService
{
    Task<(bool Success, string? Error, int RequestId, string ReferenceNumber)> CreateRequestAsync(
        CreateRequestDto dto,
        string requesterId,
        IList<(Stream Stream, string FileName, string ContentType)>? attachments = null,
        CancellationToken cancellationToken = default);

    Task<ServiceRequestDto?> GetByIdAsync(
        int id,
        string currentUserId,
        IList<string> currentRoles,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ServiceRequestListItemDto>> SearchRequestsAsync(
        RequestFilterDto filter,
        string currentUserId,
        IList<string> currentRoles,
        CancellationToken cancellationToken = default);

    Task<WorkflowResult> TransitionStatusAsync(
        int requestId,
        RequestStatus targetStatus,
        string currentUserId,
        IList<string> currentRoles,
        StatusTransitionDto dto,
        CancellationToken cancellationToken = default);

    Task<AssignmentResult> AssignRequestAsync(
        int requestId,
        string targetAgentId,
        string currentUserId,
        IList<string> currentRoles,
        string? reason = null,
        CancellationToken cancellationToken = default);

    Task<CommentResult> AddCommentAsync(
        int requestId,
        AddCommentDto dto,
        string currentUserId,
        IList<string> currentRoles,
        CancellationToken cancellationToken = default);

    Task<DuplicateCheckResult> CheckDuplicateAsync(
        int categoryId,
        string title,
        int? excludeRequestId = null,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportCsvAsync(
        RequestFilterDto filter,
        string currentUserId,
        IList<string> currentRoles,
        CancellationToken cancellationToken = default);

    Task<bool> AddCorrectionNotesAsync(
        int requestId,
        string correctionNotes,
        string currentUserId,
        CancellationToken cancellationToken = default);
}

public interface IDashboardService
{
    Task<RequesterDashboardDto> GetRequesterDashboardAsync(string userId, CancellationToken cancellationToken = default);
    Task<AgentDashboardDto> GetAgentDashboardAsync(string userId, CancellationToken cancellationToken = default);
    Task<TeamLeadDashboardDto> GetTeamLeadDashboardAsync(string userId, CancellationToken cancellationToken = default);
    Task<AdminDashboardDto> GetAdminDashboardAsync(CancellationToken cancellationToken = default);
}
