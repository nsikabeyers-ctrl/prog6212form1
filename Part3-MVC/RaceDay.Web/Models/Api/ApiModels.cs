namespace RaceDay.Web.Models.Api;

// These mirror what the API sends back. The names match the API responses exactly.

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

public record AuthResponse(int UserId, string FullName, string Email, string Role);

public record ProfileDto(
    int UserId,
    string Role,
    string FullName,
    string Email,
    string? PhoneNumber,
    DateOnly? DateOfBirth,
    string? ProfilePictureUrl);

public record CategoryDto(
    int CategoryId,
    int EventId,
    string Name,
    int? MinAge,
    int? MaxAge,
    decimal? DistanceKm);

public record EventDto(
    int EventId,
    int OrganiserId,
    string OrganiserName,
    string Name,
    string? Description,
    DateTime EventDate,
    string Location,
    decimal DistanceKm,
    EventType EventType,
    string? BannerImageUrl,
    int EnrolmentCount,
    List<CategoryDto> Categories);

public record EnrolmentDto(
    int EnrolmentId,
    int ParticipantId,
    string ParticipantName,
    int EventId,
    string EventName,
    DateTime EventDate,
    int CategoryId,
    string CategoryName,
    DateTime EnrolmentDate,
    EnrolmentStatus Status);

public record ResultDto(
    int ResultId,
    int EnrolmentId,
    int EventId,
    string EventName,
    DateTime EventDate,
    string ParticipantName,
    string CategoryName,
    TimeSpan FinishTime,
    int FinishPosition,
    int TotalFinishers);
