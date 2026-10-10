namespace ServicePortal.Application.Interfaces;

public enum SlaHealthStatus
{
    Healthy = 1,
    Approaching = 2,
    Breached = 3,
    Achieved = 4
}

public interface ISlaCalculator
{
    /// <summary>
    /// Calculates the SLA due time based on Monday-Friday 09:00 to 18:00 business hours,
    /// skipping weekends.
    /// </summary>
    DateTime CalculateDueTime(DateTime startTime, int targetHours);

    /// <summary>
    /// Determines the SLA health state (Healthy, Approaching, Breached, Achieved).
    /// </summary>
    SlaHealthStatus GetHealthStatus(DateTime dueTime, bool isResolved, DateTime? resolvedAt = null, DateTime? currentTime = null);

    /// <summary>
    /// Checks if the request is currently in a breached state.
    /// </summary>
    bool IsBreached(DateTime dueTime, bool isResolved, DateTime? resolvedAt = null, DateTime? currentTime = null);
}
