using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServicePortal.Application.DTOs;
using ServicePortal.Application.Interfaces;
using ServicePortal.Domain.Entities;
using ServicePortal.Domain.Enums;
using ServicePortal.Domain.Rules;
using ServicePortal.Infrastructure.Data;

namespace ServicePortal.Infrastructure.Services;

public class ServiceRequestService : IServiceRequestService
{
    private readonly ApplicationDbContext _context;
    private readonly ISlaCalculator _slaCalculator;
    private readonly IReferenceNumberGenerator _referenceGenerator;
    private readonly IFileStorageService _fileStorage;
    private readonly INotificationService _notificationService;
    private readonly IEmailSender _emailSender;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<ServiceRequestService> _logger;

    public ServiceRequestService(
        ApplicationDbContext context,
        ISlaCalculator slaCalculator,
        IReferenceNumberGenerator referenceGenerator,
        IFileStorageService fileStorage,
        INotificationService notificationService,
        IEmailSender emailSender,
        IAuditLogService auditLog,
        ILogger<ServiceRequestService> logger)
    {
        _context = context;
        _slaCalculator = slaCalculator;
        _referenceGenerator = referenceGenerator;
        _fileStorage = fileStorage;
        _notificationService = notificationService;
        _emailSender = emailSender;
        _auditLog = auditLog;
        _logger = logger;
    }

    public async Task<(bool Success, string? Error, int RequestId, string ReferenceNumber)> CreateRequestAsync(
        CreateRequestDto dto,
        string requesterId,
        IList<(Stream Stream, string FileName, string ContentType)>? attachments = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Verify user exists, is active, and email is confirmed (AC-01 & Section 6.1)
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == requesterId, cancellationToken);
        if (user == null || !user.IsActive)
        {
            return (false, "User account is inactive or not found.", 0, string.Empty);
        }

        if (!user.EmailConfirmed)
        {
            return (false, "Email address must be confirmed before submitting service requests.", 0, string.Empty);
        }

        // 2. Fetch category and map to SupportTeam automatically
        var category = await _context.Categories
            .Include(c => c.SupportTeam)
            .FirstOrDefaultAsync(c => c.Id == dto.CategoryId && c.IsActive, cancellationToken);

        if (category == null)
        {
            return (false, "Selected category is invalid or inactive.", 0, string.Empty);
        }

        // 3. Duplicate check check (warn if similar active request exists and user hasn't acknowledged)
        if (!dto.AcknowledgeDuplicateWarning)
        {
            var dupCheck = await CheckDuplicateAsync(dto.CategoryId, dto.Title, null, cancellationToken);
            if (dupCheck.HasDuplicate)
            {
                return (false, $"A similar active request '{dupCheck.ExistingReference}: {dupCheck.ExistingTitle}' was submitted recently. Please acknowledge duplicate warning to proceed.", 0, string.Empty);
            }
        }

        // 4. Calculate SLA targets
        var slaPolicy = await _context.SlaPolicies
            .FirstOrDefaultAsync(p => p.Priority == dto.Priority && p.IsActive, cancellationToken);

        var (respHours, resolHours) = GetDefaultOrConfiguredSlaHours(dto.Priority, slaPolicy);

        var now = DateTime.UtcNow;
        var firstResponseDue = _slaCalculator.CalculateDueTime(now, respHours);
        var resolutionDue = _slaCalculator.CalculateDueTime(now, resolHours);

        // 5. Generate concurrency-safe human-readable reference number
        var referenceNumber = await _referenceGenerator.GenerateAsync(cancellationToken);

        var request = new ServiceRequest
        {
            ReferenceNumber = referenceNumber,
            Title = dto.Title.Trim(),
            Description = dto.Description.Trim(),
            RequesterId = requesterId,
            CategoryId = category.Id,
            SupportTeamId = category.SupportTeamId,
            Priority = dto.Priority,
            Status = RequestStatus.New,
            PreferredContactMethod = dto.PreferredContactMethod,
            CreatedAt = now,
            FirstResponseDueAt = firstResponseDue,
            ResolutionDueAt = resolutionDue,
            IsSlaBreached = false
        };

        _context.ServiceRequests.Add(request);
        await _context.SaveChangesAsync(cancellationToken);

        // 6. Record initial status history
        var history = new RequestStatusHistory
        {
            ServiceRequestId = request.Id,
            FromStatus = RequestStatus.New,
            ToStatus = RequestStatus.New,
            ChangedById = requesterId,
            Reason = "Service request created.",
            CreatedAt = now
        };
        _context.RequestStatusHistories.Add(history);

        // 7. Handle optional attachments securely
        if (attachments != null && attachments.Count > 0)
        {
            foreach (var att in attachments)
            {
                if (!_fileStorage.IsAllowedExtension(att.FileName))
                {
                    continue; // Skip invalid attachments or reject
                }

                var (storedName, contentType, size) = await _fileStorage.SaveFileAsync(
                    att.Stream, att.FileName, att.ContentType, cancellationToken);

                var attachmentEntity = new RequestAttachment
                {
                    ServiceRequestId = request.Id,
                    OriginalFileName = att.FileName,
                    StoredFileName = storedName,
                    ContentType = contentType,
                    FileSizeBytes = size,
                    UploaderId = requesterId,
                    CreatedAt = now
                };
                _context.RequestAttachments.Add(attachmentEntity);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        // 8. Notify Team Leads of newly routed request
        var teamLeads = await _context.Users
            .Where(u => u.SupportTeamId == category.SupportTeamId && u.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var lead in teamLeads)
        {
            await _notificationService.CreateNotificationAsync(
                lead.Id,
                NotificationType.Assignment,
                $"New Request {request.ReferenceNumber}",
                $"New request '{request.Title}' routed to team {category.SupportTeam.Name}.",
                request.Id,
                cancellationToken);
        }

        // 9. Structured Audit Log
        await _auditLog.LogAsync(
            requesterId,
            user.DisplayName,
            "CreateRequest",
            nameof(ServiceRequest),
            request.Id.ToString(),
            $"Created request {request.ReferenceNumber} with Priority: {request.Priority}",
            null,
            null,
            cancellationToken);

        _logger.LogInformation("Successfully created ServiceRequest {Ref} for Requester {RequesterId}",
            referenceNumber, requesterId);

        return (true, null, request.Id, referenceNumber);
    }

    public async Task<ServiceRequestDto?> GetByIdAsync(
        int id,
        string currentUserId,
        IList<string> currentRoles,
        CancellationToken cancellationToken = default)
    {
        var request = await _context.ServiceRequests
            .Include(r => r.Requester)
            .Include(r => r.Category)
            .Include(r => r.SupportTeam)
            .Include(r => r.Assignee)
            .Include(r => r.Comments).ThenInclude(c => c.Author)
            .Include(r => r.Attachments).ThenInclude(a => a.Uploader)
            .Include(r => r.StatusHistories).ThenInclude(h => h.ChangedBy)
            .Include(r => r.AssignmentHistories).ThenInclude(a => a.NewAssignee)
            .Include(r => r.AssignmentHistories).ThenInclude(a => a.AssignedBy)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (request == null)
        {
            return null;
        }

        // IDOR Check & Scope check:
        // Requester can ONLY view their own request (AC-02)
        var isRequesterOnly = currentRoles.Contains(AppRoles.Requester) &&
                              !currentRoles.Contains(AppRoles.Administrator) &&
                              !currentRoles.Contains(AppRoles.TeamLead) &&
                              !currentRoles.Contains(AppRoles.SupportAgent);

        if (isRequesterOnly && request.RequesterId != currentUserId)
        {
            _logger.LogWarning("Unauthorized IDOR access attempt on Request {Id} by User {UserId}", id, currentUserId);
            return null;
        }

        // Check Support Agent team / assignment scope if role is SupportAgent only
        var isAgentOnly = currentRoles.Contains(AppRoles.SupportAgent) &&
                          !currentRoles.Contains(AppRoles.Administrator) &&
                          !currentRoles.Contains(AppRoles.TeamLead);

        if (isAgentOnly)
        {
            var currentUser = await _context.Users.FindAsync(new object[] { currentUserId }, cancellationToken);
            if (currentUser != null && request.AssigneeId != currentUserId && request.SupportTeamId != currentUser.SupportTeamId)
            {
                return null;
            }
        }

        var isTeamLeadOnly = currentRoles.Contains(AppRoles.TeamLead) && !currentRoles.Contains(AppRoles.Administrator);
        if (isTeamLeadOnly)
        {
            var currentUser = await _context.Users.FindAsync(new object[] { currentUserId }, cancellationToken);
            if (currentUser != null && request.SupportTeamId != currentUser.SupportTeamId)
            {
                return null;
            }
        }

        // Filter comments: requesters MUST NEVER see internal comments (AC-03)
        var allowedComments = request.Comments
            .Where(c => !isRequesterOnly || !c.IsInternal)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new CommentDto
            {
                Id = c.Id,
                AuthorId = c.AuthorId,
                AuthorName = c.Author.DisplayName,
                IsInternal = c.IsInternal,
                Body = c.Body,
                CreatedAt = c.CreatedAt
            })
            .ToList();

        var attachments = request.Attachments
            .OrderBy(a => a.CreatedAt)
            .Select(a => new AttachmentDto
            {
                Id = a.Id,
                OriginalFileName = a.OriginalFileName,
                StoredFileName = a.StoredFileName,
                ContentType = a.ContentType,
                FileSizeBytes = a.FileSizeBytes,
                UploaderName = a.Uploader.DisplayName,
                CreatedAt = a.CreatedAt
            })
            .ToList();

        // Build unified chronological timeline
        var timeline = BuildTimeline(request, isRequesterOnly);

        var slaHealth = _slaCalculator.GetHealthStatus(
            request.ResolutionDueAt,
            request.Status == RequestStatus.Resolved || request.Status == RequestStatus.Closed,
            request.ResolvedAt);

        return new ServiceRequestDto
        {
            Id = request.Id,
            ReferenceNumber = request.ReferenceNumber,
            Title = request.Title,
            Description = request.Description,
            RequesterId = request.RequesterId,
            RequesterName = request.Requester.DisplayName,
            RequesterEmail = request.Requester.Email ?? string.Empty,
            CategoryId = request.CategoryId,
            CategoryName = request.Category.Name,
            SupportTeamId = request.SupportTeamId,
            SupportTeamName = request.SupportTeam.Name,
            AssigneeId = request.AssigneeId,
            AssigneeName = request.Assignee?.DisplayName,
            Priority = request.Priority,
            Status = request.Status,
            PreferredContactMethod = request.PreferredContactMethod,
            CreatedAt = request.CreatedAt,
            UpdatedAt = request.UpdatedAt,
            FirstResponseDueAt = request.FirstResponseDueAt,
            FirstRespondedAt = request.FirstRespondedAt,
            ResolutionDueAt = request.ResolutionDueAt,
            ResolvedAt = request.ResolvedAt,
            ClosedAt = request.ClosedAt,
            IsSlaBreached = request.IsSlaBreached,
            SlaBreachedAt = request.SlaBreachedAt,
            SlaHealth = slaHealth,
            ResolutionSummary = request.ResolutionSummary,
            ResolutionCategory = request.ResolutionCategory,
            ReopenReason = request.ReopenReason,
            ReopenedAt = request.ReopenedAt,
            CorrectionNotes = request.CorrectionNotes,
            RowVersion = request.RowVersion,
            Comments = allowedComments,
            Attachments = attachments,
            Timeline = timeline
        };
    }

    public async Task<PagedResult<ServiceRequestListItemDto>> SearchRequestsAsync(
        RequestFilterDto filter,
        string currentUserId,
        IList<string> currentRoles,
        CancellationToken cancellationToken = default)
    {
        var query = BuildAuthorizedQuery(filter, currentUserId, currentRoles);

        var totalCount = await query.CountAsync(cancellationToken);

        // Sorting
        query = ApplySorting(query, filter.SortBy, filter.SortDescending);

        // Server-side Pagination
        var pageSize = Math.Clamp(filter.PageSize, 5, 100);
        var pageNumber = Math.Max(1, filter.PageNumber);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ServiceRequestListItemDto
            {
                Id = r.Id,
                ReferenceNumber = r.ReferenceNumber,
                Title = r.Title,
                CategoryName = r.Category.Name,
                SupportTeamName = r.SupportTeam.Name,
                RequesterName = r.Requester.DisplayName,
                AssigneeName = r.Assignee != null ? r.Assignee.DisplayName : null,
                Priority = r.Priority,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                ResolutionDueAt = r.ResolutionDueAt,
                IsSlaBreached = r.IsSlaBreached,
                CommentCount = r.Comments.Count,
                AttachmentCount = r.Attachments.Count
            })
            .ToListAsync(cancellationToken);

        // Calculate SLA health for each item
        foreach (var item in items)
        {
            var isResolved = item.Status == RequestStatus.Resolved || item.Status == RequestStatus.Closed;
            item.SlaHealth = _slaCalculator.GetHealthStatus(item.ResolutionDueAt, isResolved);
        }

        return new PagedResult<ServiceRequestListItemDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<WorkflowResult> TransitionStatusAsync(
        int requestId,
        RequestStatus targetStatus,
        string currentUserId,
        IList<string> currentRoles,
        StatusTransitionDto dto,
        CancellationToken cancellationToken = default)
    {
        var request = await _context.ServiceRequests
            .Include(r => r.Requester)
            .Include(r => r.Assignee)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (request == null)
        {
            return WorkflowResult.Failure("Service request not found.");
        }

        var actorUser = await _context.Users.FindAsync(new object[] { currentUserId }, cancellationToken);
        var actorRole = DetermineEffectiveRole(currentRoles, request, currentUserId);

        // Validate transition using domain state machine (AC-04)
        var validation = WorkflowValidator.ValidateTransition(
            request,
            targetStatus,
            actorRole,
            currentUserId,
            dto.Comment,
            dto.ResolutionSummary,
            dto.ResolutionCategory,
            dto.ReopenReason);

        if (!validation.IsSuccess)
        {
            return validation;
        }

        // Optimistic concurrency verification (AC-10)
        if (dto.RowVersion.Length > 0 && !request.RowVersion.SequenceEqual(dto.RowVersion))
        {
            return WorkflowResult.Failure("The request was modified by another user concurrently. Please reload and review the latest changes.");
        }

        var oldStatus = request.Status;
        var now = DateTime.UtcNow;

        request.Status = targetStatus;
        request.UpdatedAt = now;

        // Specific status side effects
        if (oldStatus == RequestStatus.Assigned && targetStatus == RequestStatus.InProgress && !request.FirstRespondedAt.HasValue)
        {
            request.FirstRespondedAt = now;
        }

        if (targetStatus == RequestStatus.Resolved)
        {
            request.ResolutionSummary = dto.ResolutionSummary;
            request.ResolutionCategory = dto.ResolutionCategory;
            request.ResolvedAt = now;
        }
        else if (targetStatus == RequestStatus.Closed)
        {
            request.ClosedAt = now;
        }
        else if (oldStatus == RequestStatus.Resolved && targetStatus == RequestStatus.InProgress)
        {
            // Reopened
            request.ReopenReason = dto.ReopenReason;
            request.ReopenedAt = now;
            request.ResolvedAt = null;
        }

        // Record Status History
        var history = new RequestStatusHistory
        {
            ServiceRequestId = request.Id,
            FromStatus = oldStatus,
            ToStatus = targetStatus,
            ChangedById = currentUserId,
            Reason = dto.Comment ?? dto.ReopenReason ?? dto.ResolutionSummary,
            CreatedAt = now
        };
        _context.RequestStatusHistories.Add(history);

        // If a transition comment was provided (e.g., Waiting for Requester), record it
        if (!string.IsNullOrWhiteSpace(dto.Comment))
        {
            var comment = new RequestComment
            {
                ServiceRequestId = request.Id,
                AuthorId = currentUserId,
                IsInternal = false, // Must be public as required by AC
                Body = dto.Comment.Trim(),
                CreatedAt = now
            };
            _context.RequestComments.Add(comment);
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return WorkflowResult.Failure("Conflict detected: This service request was updated concurrently by another user. Your changes were not applied.");
        }

        // Notifications
        var notifyRecipient = currentUserId == request.RequesterId ? request.AssigneeId : request.RequesterId;
        if (!string.IsNullOrEmpty(notifyRecipient))
        {
            await _notificationService.CreateNotificationAsync(
                notifyRecipient,
                targetStatus == RequestStatus.InProgress && oldStatus == RequestStatus.Resolved ? NotificationType.Reopened : NotificationType.StatusChange,
                $"Status Updated: {request.ReferenceNumber}",
                $"Request status changed from {oldStatus} to {targetStatus}.",
                request.Id,
                cancellationToken);
        }

        // Audit Log
        await _auditLog.LogAsync(
            currentUserId,
            actorUser?.DisplayName ?? "System",
            "StatusTransition",
            nameof(ServiceRequest),
            request.Id.ToString(),
            $"Transitioned status from {oldStatus} to {targetStatus}.",
            null,
            null,
            cancellationToken);

        return WorkflowResult.Success();
    }

    public async Task<AssignmentResult> AssignRequestAsync(
        int requestId,
        string targetAgentId,
        string currentUserId,
        IList<string> currentRoles,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        // Only Team Lead or Admin can assign/reassign (AC-05 & Section 6.3)
        var isAdmin = currentRoles.Contains(AppRoles.Administrator);
        var isTeamLead = currentRoles.Contains(AppRoles.TeamLead);

        if (!isAdmin && !isTeamLead)
        {
            return AssignmentResult.Failure("Only a Team Lead or Administrator can assign or reassign service requests.");
        }

        var request = await _context.ServiceRequests
            .Include(r => r.SupportTeam)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (request == null)
        {
            return AssignmentResult.Failure("Service request not found.");
        }

        if (request.Status == RequestStatus.Closed)
        {
            return AssignmentResult.Failure("Closed service requests cannot be reassigned.");
        }

        // Target agent must be active and in the request's assigned support team (AC-05)
        var targetAgent = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == targetAgentId && u.IsActive, cancellationToken);

        if (targetAgent == null)
        {
            return AssignmentResult.Failure("The selected support agent is invalid or inactive.");
        }

        if (targetAgent.SupportTeamId != request.SupportTeamId)
        {
            return AssignmentResult.Failure($"Agent '{targetAgent.DisplayName}' does not belong to the request's team ('{request.SupportTeam.Name}'). Team leads can only assign to members of their team.");
        }

        var previousAssigneeId = request.AssigneeId;
        request.AssigneeId = targetAgentId;
        request.UpdatedAt = DateTime.UtcNow;

        if (request.Status == RequestStatus.New)
        {
            request.Status = RequestStatus.Assigned;
            _context.RequestStatusHistories.Add(new RequestStatusHistory
            {
                ServiceRequestId = request.Id,
                FromStatus = RequestStatus.New,
                ToStatus = RequestStatus.Assigned,
                ChangedById = currentUserId,
                Reason = reason ?? $"Assigned to {targetAgent.DisplayName}",
                CreatedAt = DateTime.UtcNow
            });
        }

        var assignmentHistory = new AssignmentHistory
        {
            ServiceRequestId = request.Id,
            PreviousAssigneeId = previousAssigneeId,
            NewAssigneeId = targetAgentId,
            AssignedById = currentUserId,
            Reason = reason,
            CreatedAt = DateTime.UtcNow
        };
        _context.AssignmentHistories.Add(assignmentHistory);

        await _context.SaveChangesAsync(cancellationToken);

        // Notify newly assigned agent
        await _notificationService.CreateNotificationAsync(
            targetAgentId,
            NotificationType.Assignment,
            $"Assigned Request {request.ReferenceNumber}",
            $"You have been assigned to service request '{request.Title}'.",
            request.Id,
            cancellationToken);

        await _auditLog.LogAsync(
            currentUserId,
            "Actor",
            "AssignRequest",
            nameof(ServiceRequest),
            request.Id.ToString(),
            $"Assigned request {request.ReferenceNumber} to {targetAgent.DisplayName}",
            null,
            null,
            cancellationToken);

        return AssignmentResult.Success();
    }

    public async Task<CommentResult> AddCommentAsync(
        int requestId,
        AddCommentDto dto,
        string currentUserId,
        IList<string> currentRoles,
        CancellationToken cancellationToken = default)
    {
        var request = await _context.ServiceRequests
            .Include(r => r.Requester)
            .Include(r => r.Assignee)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (request == null)
        {
            return CommentResult.Failure("Service request not found.");
        }

        if (request.Status == RequestStatus.Closed)
        {
            return CommentResult.Failure("Comments cannot be added to closed service requests.");
        }

        var isRequester = currentRoles.Contains(AppRoles.Requester) &&
                          !currentRoles.Contains(AppRoles.Administrator) &&
                          !currentRoles.Contains(AppRoles.TeamLead) &&
                          !currentRoles.Contains(AppRoles.SupportAgent);

        if (isRequester)
        {
            if (request.RequesterId != currentUserId)
            {
                return CommentResult.Failure("You are not authorized to comment on this request.");
            }
            // Requesters can NEVER create internal comments
            dto.IsInternal = false;
        }

        var comment = new RequestComment
        {
            ServiceRequestId = request.Id,
            AuthorId = currentUserId,
            IsInternal = dto.IsInternal,
            Body = dto.Body.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.RequestComments.Add(comment);
        request.UpdatedAt = DateTime.UtcNow;

        // If request was WaitingForRequester and Requester comments, automatically advance to InProgress
        if (request.Status == RequestStatus.WaitingForRequester && currentUserId == request.RequesterId)
        {
            request.Status = RequestStatus.InProgress;
            _context.RequestStatusHistories.Add(new RequestStatusHistory
            {
                ServiceRequestId = request.Id,
                FromStatus = RequestStatus.WaitingForRequester,
                ToStatus = RequestStatus.InProgress,
                ChangedById = currentUserId,
                Reason = "Requester provided additional information.",
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Notify recipient if public comment
        if (!dto.IsInternal)
        {
            var recipient = currentUserId == request.RequesterId ? request.AssigneeId : request.RequesterId;
            if (!string.IsNullOrEmpty(recipient))
            {
                await _notificationService.CreateNotificationAsync(
                    recipient,
                    NotificationType.Comment,
                    $"New Comment on {request.ReferenceNumber}",
                    $"A new comment was posted on request '{request.Title}'.",
                    request.Id,
                    cancellationToken);
            }
        }

        return CommentResult.Success(comment.Id);
    }

    public async Task<DuplicateCheckResult> CheckDuplicateAsync(
        int categoryId,
        string title,
        int? excludeRequestId = null,
        CancellationToken cancellationToken = default)
    {
        var cleanedTitle = title.Trim().ToLower();
        var recentWindow = DateTime.UtcNow.AddDays(-7);

        var query = _context.ServiceRequests
            .AsNoTracking()
            .Where(r => r.CategoryId == categoryId &&
                        r.Status != RequestStatus.Closed &&
                        r.CreatedAt >= recentWindow);

        if (excludeRequestId.HasValue)
        {
            query = query.Where(r => r.Id != excludeRequestId.Value);
        }

        var existing = await query
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(r => r.Title.ToLower() == cleanedTitle ||
                                      r.Title.ToLower().Contains(cleanedTitle) ||
                                      cleanedTitle.Contains(r.Title.ToLower()),
                                  cancellationToken);

        if (existing != null)
        {
            return new DuplicateCheckResult
            {
                HasDuplicate = true,
                ExistingReference = existing.ReferenceNumber,
                ExistingTitle = existing.Title,
                ExistingCreatedAt = existing.CreatedAt
            };
        }

        return new DuplicateCheckResult { HasDuplicate = false };
    }

    public async Task<byte[]> ExportCsvAsync(
        RequestFilterDto filter,
        string currentUserId,
        IList<string> currentRoles,
        CancellationToken cancellationToken = default)
    {
        // Enforce identical authorization filters as search view (AC-09)
        var query = BuildAuthorizedQuery(filter, currentUserId, currentRoles);
        query = ApplySorting(query, filter.SortBy, filter.SortDescending);

        var rows = await query
            .Select(r => new
            {
                r.ReferenceNumber,
                r.Title,
                Category = r.Category.Name,
                Team = r.SupportTeam.Name,
                Requester = r.Requester.DisplayName,
                Assignee = r.Assignee != null ? r.Assignee.DisplayName : "Unassigned",
                Priority = r.Priority.ToString(),
                Status = r.Status.ToString(),
                CreatedAt = r.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                ResolutionDueAt = r.ResolutionDueAt.ToString("yyyy-MM-dd HH:mm:ss"),
                IsSlaBreached = r.IsSlaBreached ? "Yes" : "No",
                ResolvedAt = r.ResolvedAt.HasValue ? r.ResolvedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : ""
            })
            .ToListAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("Reference,Title,Category,Team,Requester,Assignee,Priority,Status,CreatedAt,ResolutionDueAt,SlaBreached,ResolvedAt");

        foreach (var r in rows)
        {
            sb.AppendLine($"\"{EscapeCsv(r.ReferenceNumber)}\",\"{EscapeCsv(r.Title)}\",\"{EscapeCsv(r.Category)}\",\"{EscapeCsv(r.Team)}\",\"{EscapeCsv(r.Requester)}\",\"{EscapeCsv(r.Assignee)}\",\"{r.Priority}\",\"{r.Status}\",\"{r.CreatedAt}\",\"{r.ResolutionDueAt}\",\"{r.IsSlaBreached}\",\"{r.ResolvedAt}\"");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public async Task<bool> AddCorrectionNotesAsync(
        int requestId,
        string correctionNotes,
        string currentUserId,
        CancellationToken cancellationToken = default)
    {
        var request = await _context.ServiceRequests.FindAsync(new object[] { requestId }, cancellationToken);
        if (request == null || request.Status != RequestStatus.Closed)
        {
            return false;
        }

        request.CorrectionNotes = correctionNotes;
        request.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditLog.LogAsync(
            currentUserId,
            "Administrator",
            "AddCorrectionNotes",
            nameof(ServiceRequest),
            requestId.ToString(),
            "Administrator added post-closure correction notes.",
            null,
            null,
            cancellationToken);

        return true;
    }

    #region Helper Methods

    private IQueryable<ServiceRequest> BuildAuthorizedQuery(
        RequestFilterDto filter,
        string currentUserId,
        IList<string> currentRoles)
    {
        var query = _context.ServiceRequests
            .Include(r => r.Category)
            .Include(r => r.SupportTeam)
            .Include(r => r.Requester)
            .Include(r => r.Assignee)
            .AsNoTracking();

        // 1. Role-based scoping (AC-02 & AC-08)
        var isAdmin = currentRoles.Contains(AppRoles.Administrator);
        var isTeamLead = currentRoles.Contains(AppRoles.TeamLead);
        var isAgent = currentRoles.Contains(AppRoles.SupportAgent);
        var isRequester = currentRoles.Contains(AppRoles.Requester);

        if (!isAdmin)
        {
            if (isRequester && !isTeamLead && !isAgent)
            {
                // Requesters can ONLY ever see their own requests!
                query = query.Where(r => r.RequesterId == currentUserId);
            }
            else if (isTeamLead)
            {
                var userTeamId = _context.Users
                    .Where(u => u.Id == currentUserId)
                    .Select(u => u.SupportTeamId)
                    .FirstOrDefault();

                query = query.Where(r => r.SupportTeamId == userTeamId || r.RequesterId == currentUserId);
            }
            else if (isAgent)
            {
                var userTeamId = _context.Users
                    .Where(u => u.Id == currentUserId)
                    .Select(u => u.SupportTeamId)
                    .FirstOrDefault();

                query = query.Where(r => r.AssigneeId == currentUserId || r.SupportTeamId == userTeamId || r.RequesterId == currentUserId);
            }
        }

        // 2. Filters
        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            query = query.Where(r => r.ReferenceNumber.ToLower().Contains(term) ||
                                     r.Title.ToLower().Contains(term) ||
                                     r.Description.ToLower().Contains(term) ||
                                     r.Requester.DisplayName.ToLower().Contains(term) ||
                                     (r.Requester.Email != null && r.Requester.Email.ToLower().Contains(term)));
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(r => r.Status == filter.Status.Value);
        }

        if (filter.CategoryId.HasValue)
        {
            query = query.Where(r => r.CategoryId == filter.CategoryId.Value);
        }

        if (filter.Priority.HasValue)
        {
            query = query.Where(r => r.Priority == filter.Priority.Value);
        }

        if (filter.TeamId.HasValue)
        {
            query = query.Where(r => r.SupportTeamId == filter.TeamId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.AssigneeId))
        {
            query = query.Where(r => r.AssigneeId == filter.AssigneeId);
        }

        if (!string.IsNullOrWhiteSpace(filter.RequesterId))
        {
            query = query.Where(r => r.RequesterId == filter.RequesterId);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(r => r.CreatedAt >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(r => r.CreatedAt <= filter.ToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SlaState))
        {
            var now = DateTime.UtcNow;
            if (filter.SlaState.Equals("breached", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(r => r.IsSlaBreached || (r.Status != RequestStatus.Resolved && r.Status != RequestStatus.Closed && r.ResolutionDueAt < now));
            }
            else if (filter.SlaState.Equals("approaching", StringComparison.OrdinalIgnoreCase))
            {
                var threshold = now.AddHours(2);
                query = query.Where(r => !r.IsSlaBreached &&
                                         r.Status != RequestStatus.Resolved &&
                                         r.Status != RequestStatus.Closed &&
                                         r.ResolutionDueAt >= now &&
                                         r.ResolutionDueAt <= threshold);
            }
        }

        return query;
    }

    private static IQueryable<ServiceRequest> ApplySorting(IQueryable<ServiceRequest> query, string? sortBy, bool descending)
    {
        return (sortBy?.ToLower()) switch
        {
            "reference" => descending ? query.OrderByDescending(r => r.ReferenceNumber) : query.OrderBy(r => r.ReferenceNumber),
            "priority" => descending ? query.OrderByDescending(r => r.Priority) : query.OrderBy(r => r.Priority),
            "status" => descending ? query.OrderByDescending(r => r.Status) : query.OrderBy(r => r.Status),
            "sladue" => descending ? query.OrderByDescending(r => r.ResolutionDueAt) : query.OrderBy(r => r.ResolutionDueAt),
            _ => descending ? query.OrderByDescending(r => r.CreatedAt) : query.OrderBy(r => r.CreatedAt)
        };
    }

    private static List<TimelineEventDto> BuildTimeline(ServiceRequest request, bool isRequester)
    {
        var events = new List<TimelineEventDto>();

        // Status history
        foreach (var sh in request.StatusHistories)
        {
            events.Add(new TimelineEventDto
            {
                EventType = "StatusChange",
                Title = $"Status: {sh.ToStatus}",
                Description = sh.Reason ?? $"Status changed from {sh.FromStatus} to {sh.ToStatus}",
                ActorName = sh.ChangedBy?.DisplayName ?? "System",
                Timestamp = sh.CreatedAt,
                BadgeClass = "bg-primary",
                IsInternal = false
            });
        }

        // Assignments
        foreach (var ah in request.AssignmentHistories)
        {
            events.Add(new TimelineEventDto
            {
                EventType = "Assignment",
                Title = "Assignment Changed",
                Description = $"Assigned to {ah.NewAssignee.DisplayName}" + (ah.Reason != null ? $" (Reason: {ah.Reason})" : ""),
                ActorName = ah.AssignedBy.DisplayName,
                Timestamp = ah.CreatedAt,
                BadgeClass = "bg-info",
                IsInternal = false
            });
        }

        // Comments (filter internal if requester)
        foreach (var c in request.Comments.Where(c => !isRequester || !c.IsInternal))
        {
            events.Add(new TimelineEventDto
            {
                EventType = "Comment",
                Title = c.IsInternal ? "Internal Note" : "Public Comment",
                Description = c.Body,
                ActorName = c.Author.DisplayName,
                Timestamp = c.CreatedAt,
                BadgeClass = c.IsInternal ? "bg-warning text-dark" : "bg-secondary",
                IsInternal = c.IsInternal
            });
        }

        // SLA breach event if applicable
        if (request.IsSlaBreached && request.SlaBreachedAt.HasValue)
        {
            events.Add(new TimelineEventDto
            {
                EventType = "SlaBreach",
                Title = "SLA Breach Detected",
                Description = $"Resolution due target ({request.ResolutionDueAt:g}) was breached.",
                ActorName = "System SLA Monitor",
                Timestamp = request.SlaBreachedAt.Value,
                BadgeClass = "bg-danger",
                IsInternal = false
            });
        }

        return events.OrderByDescending(e => e.Timestamp).ToList();
    }

    private static (int RespHours, int ResolHours) GetDefaultOrConfiguredSlaHours(RequestPriority priority, SlaPolicy? policy)
    {
        if (policy != null)
        {
            return (policy.ResponseTargetHours, policy.ResolutionTargetHours);
        }

        return priority switch
        {
            RequestPriority.Critical => (1, 4),
            RequestPriority.High => (4, 8),
            RequestPriority.Medium => (8, 24),
            _ => (48, 96)
        };
    }

    private static string DetermineEffectiveRole(IList<string> roles, ServiceRequest request, string currentUserId)
    {
        if (roles.Contains(AppRoles.Administrator)) return AppRoles.Administrator;
        if (roles.Contains(AppRoles.TeamLead)) return AppRoles.TeamLead;
        if (roles.Contains(AppRoles.SupportAgent)) return AppRoles.SupportAgent;
        return AppRoles.Requester;
    }

    private static string EscapeCsv(string? value) => value?.Replace("\"", "\"\"") ?? string.Empty;

    #endregion
}
