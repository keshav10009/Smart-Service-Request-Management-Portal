namespace ServicePortal.Application.Interfaces;

public interface IReferenceNumberGenerator
{
    /// <summary>
    /// Atomically generates a unique human-readable reference number
    /// adhering to format SR-YYYY-NNNNNN (e.g., SR-2026-000101).
    /// Safe under high concurrency.
    /// </summary>
    Task<string> GenerateAsync(CancellationToken cancellationToken = default);
}
