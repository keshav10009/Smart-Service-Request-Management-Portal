namespace ServicePortal.Domain.Enums;

public enum RequestPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum RequestStatus
{
    New = 1,
    Assigned = 2,
    InProgress = 3,
    WaitingForRequester = 4,
    Resolved = 5,
    Closed = 6
}

public enum PreferredContactMethod
{
    Email = 1,
    Phone = 2,
    Portal = 3
}

public enum NotificationType
{
    Assignment = 1,
    Comment = 2,
    StatusChange = 3,
    SlaBreach = 4,
    Reopened = 5,
    System = 6
}

public enum ResolutionCategory
{
    PermanentFix = 1,
    Workaround = 2,
    ConfigurationChange = 3,
    UserEducation = 4,
    CannotReproduce = 5,
    Duplicate = 6
}

public static class AppRoles
{
    public const string Administrator = "Administrator";
    public const string TeamLead = "Team Lead";
    public const string SupportAgent = "Support Agent";
    public const string Requester = "Requester";

    public static readonly string[] AllRoles = [Administrator, TeamLead, SupportAgent, Requester];
}
