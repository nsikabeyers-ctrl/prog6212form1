namespace RaceDay.Api.Models;

/// <summary>The two roles in the system. Stored in the session as plain strings.</summary>
public static class Roles
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
}

// Values start at 1 so that a missing value (0) is never a valid choice.
public enum EventType
{
    Run = 1,
    Walk = 2,
    Cycle = 3
}

public enum EnrolmentStatus
{
    Pending = 1,
    Confirmed = 2,
    Cancelled = 3
}
