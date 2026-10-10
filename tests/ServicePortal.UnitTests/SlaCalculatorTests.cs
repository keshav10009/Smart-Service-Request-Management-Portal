using ServicePortal.Application.Interfaces;
using ServicePortal.Application.Services;
using Xunit;

namespace ServicePortal.UnitTests.Sla;

public class SlaCalculatorTests
{
    private readonly SlaCalculator _calculator = new();

    [Fact]
    public void CalculateDueTime_WithinSameBusinessDay_CalculatesAccurately()
    {
        // Monday 10:00 + 4 hours = Monday 14:00
        var start = new DateTime(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc); // Monday
        var due = _calculator.CalculateDueTime(start, 4);

        Assert.Equal(new DateTime(2026, 10, 12, 14, 0, 0, DateTimeKind.Utc), due);
    }

    [Fact]
    public void CalculateDueTime_Overnight_AdvancesToNextBusinessDay()
    {
        // Monday 16:00 + 4 hours (2 hours Mon 16-18, remaining 2 hours Tue 09-11)
        var start = new DateTime(2026, 10, 12, 16, 0, 0, DateTimeKind.Utc); // Monday
        var due = _calculator.CalculateDueTime(start, 4);

        Assert.Equal(new DateTime(2026, 10, 13, 11, 0, 0, DateTimeKind.Utc), due);
    }

    [Fact]
    public void CalculateDueTime_FridayAfternoon_SkipsWeekendEntirely()
    {
        // Friday 16:00 + 4 hours -> Fri 16-18 (2 hrs) + Mon 09-11 (2 hrs)
        var start = new DateTime(2026, 10, 16, 16, 0, 0, DateTimeKind.Utc); // Friday
        var due = _calculator.CalculateDueTime(start, 4);

        Assert.Equal(new DateTime(2026, 10, 19, 11, 0, 0, DateTimeKind.Utc), due); // Monday
    }

    [Fact]
    public void CalculateDueTime_InitiatedOnSaturday_SnapsToMondayMorning()
    {
        // Saturday 14:00 + 2 hours -> Snaps to Monday 09:00 + 2 hours = Monday 11:00
        var start = new DateTime(2026, 10, 17, 14, 0, 0, DateTimeKind.Utc); // Saturday
        var due = _calculator.CalculateDueTime(start, 2);

        Assert.Equal(new DateTime(2026, 10, 19, 11, 0, 0, DateTimeKind.Utc), due);
    }

    [Fact]
    public void CalculateDueTime_InitiatedBeforeBusinessHours_SnapsToNineAm()
    {
        // Wednesday 07:30 + 3 hours -> Snaps to Wed 09:00 + 3 hours = Wed 12:00
        var start = new DateTime(2026, 10, 14, 7, 30, 0, DateTimeKind.Utc); // Wednesday
        var due = _calculator.CalculateDueTime(start, 3);

        Assert.Equal(new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc), due);
    }

    [Fact]
    public void CalculateDueTime_InitiatedAfterBusinessHours_RollsToNextDayNineAm()
    {
        // Tuesday 19:45 + 2 hours -> Rolls to Wednesday 09:00 + 2 hours = Wed 11:00
        var start = new DateTime(2026, 10, 13, 19, 45, 0, DateTimeKind.Utc); // Tuesday
        var due = _calculator.CalculateDueTime(start, 2);

        Assert.Equal(new DateTime(2026, 10, 14, 11, 0, 0, DateTimeKind.Utc), due);
    }

    [Fact]
    public void CalculateDueTime_ZeroOrNegativeHours_ThrowsArgumentException()
    {
        var start = new DateTime(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc);
        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.CalculateDueTime(start, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.CalculateDueTime(start, -5));
    }

    [Fact]
    public void GetHealthStatus_WhenPlentyOfTimeRemains_ReturnsHealthy()
    {
        var due = new DateTime(2026, 10, 12, 17, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc);

        var status = _calculator.GetHealthStatus(due, isResolved: false, currentTime: now);
        Assert.Equal(SlaHealthStatus.Healthy, status);
    }

    [Fact]
    public void GetHealthStatus_WhenWithinTwoHours_ReturnsApproaching()
    {
        var due = new DateTime(2026, 10, 12, 17, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 10, 12, 15, 30, 0, DateTimeKind.Utc); // 1.5 hours left

        var status = _calculator.GetHealthStatus(due, isResolved: false, currentTime: now);
        Assert.Equal(SlaHealthStatus.Approaching, status);
    }

    [Fact]
    public void GetHealthStatus_WhenPastDue_ReturnsBreached()
    {
        var due = new DateTime(2026, 10, 12, 14, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 10, 12, 14, 30, 0, DateTimeKind.Utc); // 30 mins past

        var status = _calculator.GetHealthStatus(due, isResolved: false, currentTime: now);
        Assert.Equal(SlaHealthStatus.Breached, status);
    }

    [Fact]
    public void GetHealthStatus_WhenResolvedBeforeDeadline_ReturnsAchieved()
    {
        var due = new DateTime(2026, 10, 12, 17, 0, 0, DateTimeKind.Utc);
        var resolved = new DateTime(2026, 10, 12, 15, 0, 0, DateTimeKind.Utc);

        var status = _calculator.GetHealthStatus(due, isResolved: true, resolvedAt: resolved);
        Assert.Equal(SlaHealthStatus.Achieved, status);
    }
}
