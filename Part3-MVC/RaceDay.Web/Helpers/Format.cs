using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Helpers;

/// <summary>Small helpers the views use to show things the same way on every page.</summary>
public static class Format
{
    public static string Date(DateTime value) => value.ToString("ddd, d MMM yyyy");

    public static string DateTimeFull(DateTime value) => value.ToString("ddd, d MMM yyyy 'at' HH:mm");

    public static string Time(TimeSpan value) => value.ToString(@"hh\:mm\:ss");

    public static string Km(decimal value) => value % 1 == 0 ? $"{value:0} km" : $"{value:0.##} km";

    /// <summary>12 becomes 12th, 21 becomes 21st.</summary>
    public static string Ordinal(int number)
    {
        var lastTwo = number % 100;
        if (lastTwo is >= 11 and <= 13) return $"{number}th";

        return (number % 10) switch
        {
            1 => $"{number}st",
            2 => $"{number}nd",
            3 => $"{number}rd",
            _ => $"{number}th"
        };
    }

    public static string AgeRange(CategoryDto c)
    {
        if (c.MinAge is not null && c.MaxAge is not null) return $"Ages {c.MinAge} to {c.MaxAge}";
        if (c.MinAge is not null) return $"Ages {c.MinAge} and up";
        if (c.MaxAge is not null) return $"Up to age {c.MaxAge}";
        return "All ages";
    }

    public static string TypeIcon(EventType type) => type switch
    {
        EventType.Run => "/img/run.svg",
        EventType.Walk => "/img/walk.svg",
        _ => "/img/cycle.svg"
    };

    public static string TypeClass(EventType type) => type switch
    {
        EventType.Run => "type-run",
        EventType.Walk => "type-walk",
        _ => "type-cycle"
    };

    public static string StatusClass(EnrolmentStatus status) => status switch
    {
        EnrolmentStatus.Confirmed => "status-confirmed",
        EnrolmentStatus.Pending => "status-pending",
        _ => "status-cancelled"
    };
}
