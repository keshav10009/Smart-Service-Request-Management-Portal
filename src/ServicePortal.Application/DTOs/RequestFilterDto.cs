using ServicePortal.Domain.Enums;

namespace ServicePortal.Application.DTOs;

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}

public class RequestFilterDto
{
    public string? SearchTerm { get; set; }
    public RequestStatus? Status { get; set; }
    public int? CategoryId { get; set; }
    public RequestPriority? Priority { get; set; }
    public int? TeamId { get; set; }
    public string? AssigneeId { get; set; }
    public string? RequesterId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SlaState { get; set; } // "breached", "approaching", "healthy"
    public string? SortBy { get; set; } = "CreatedAt";
    public bool SortDescending { get; set; } = true;
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
