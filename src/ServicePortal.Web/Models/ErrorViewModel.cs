namespace ServicePortal.Web.Models;

public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public string? CorrelationId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    public bool ShowCorrelationId => !string.IsNullOrEmpty(CorrelationId);
}
