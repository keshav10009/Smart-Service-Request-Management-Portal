using ServicePortal.Application.Interfaces;

namespace ServicePortal.Application.Services;

public class SlaCalculator : ISlaCalculator
{
    public const int BusinessStartHour = 9;
    public const int BusinessEndHour = 18;
    public const int BusinessHoursPerDay = BusinessEndHour - BusinessStartHour; // 9 hours
    public const double ApproachingThresholdHours = 2.0;

    public DateTime CalculateDueTime(DateTime startTime, int targetHours)
    {
        if (targetHours <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetHours), "Target hours must be greater than zero.");
        }

        var current = SnapToBusinessHours(startTime);
        var remainingHours = (double)targetHours;

        while (remainingHours > 0)
        {
            var endOfToday = current.Date.AddHours(BusinessEndHour);
            var availableToday = (endOfToday - current).TotalHours;

            if (remainingHours <= availableToday)
            {
                current = current.AddHours(remainingHours);
                remainingHours = 0;
            }
            else
            {
                remainingHours -= availableToday;
                current = current.Date.AddDays(1).AddHours(BusinessStartHour);
                current = AdvancePastWeekend(current);
            }
        }

        return current;
    }

    public SlaHealthStatus GetHealthStatus(DateTime dueTime, bool isResolved, DateTime? resolvedAt = null, DateTime? currentTime = null)
    {
        var now = currentTime ?? DateTime.UtcNow;

        if (isResolved)
        {
            var resolutionTime = resolvedAt ?? now;
            return resolutionTime <= dueTime ? SlaHealthStatus.Achieved : SlaHealthStatus.Breached;
        }

        if (now > dueTime)
        {
            return SlaHealthStatus.Breached;
        }

        if ((dueTime - now).TotalHours <= ApproachingThresholdHours)
        {
            return SlaHealthStatus.Approaching;
        }

        return SlaHealthStatus.Healthy;
    }

    public bool IsBreached(DateTime dueTime, bool isResolved, DateTime? resolvedAt = null, DateTime? currentTime = null)
    {
        var status = GetHealthStatus(dueTime, isResolved, resolvedAt, currentTime);
        return status == SlaHealthStatus.Breached;
    }

    public static DateTime SnapToBusinessHours(DateTime dt)
    {
        var current = AdvancePastWeekend(dt);

        if (current.Hour < BusinessStartHour)
        {
            return current.Date.AddHours(BusinessStartHour);
        }

        if (current.Hour >= BusinessEndHour)
        {
            current = current.Date.AddDays(1).AddHours(BusinessStartHour);
            return AdvancePastWeekend(current);
        }

        return current;
    }

    private static DateTime AdvancePastWeekend(DateTime dt)
    {
        var current = dt;
        while (current.DayOfWeek == DayOfWeek.Saturday || current.DayOfWeek == DayOfWeek.Sunday)
        {
            current = current.Date.AddDays(1).AddHours(BusinessStartHour);
        }
        return current;
    }
}
