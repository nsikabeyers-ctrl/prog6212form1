using RaceDay.Api.Models;

namespace RaceDay.Api.DTOs;

public record AuthResponse(int UserId, string FullName, string Email, string Role);

public record ProfileResponse(
    int UserId,
    string Role,
    string FullName,
    string Email,
    string? PhoneNumber,
    DateOnly? DateOfBirth,
    string? ProfilePictureUrl);

public record CategoryResponse(
    int CategoryId,
    int EventId,
    string Name,
    int? MinAge,
    int? MaxAge,
    decimal? DistanceKm);

public record EventResponse(
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
    List<CategoryResponse> Categories);

public record EnrolmentResponse(
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

public record ResultResponse(
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
