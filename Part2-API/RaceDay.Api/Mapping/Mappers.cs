using RaceDay.Api.DTOs;
using RaceDay.Api.Models;

namespace RaceDay.Api.Mapping;

/// <summary>Turns entities into the shapes the API returns. Callers must Include the navigation properties used here.</summary>
public static class Mappers
{
    public static CategoryResponse ToResponse(this Category c) =>
        new(c.CategoryId, c.EventId, c.Name, c.MinAge, c.MaxAge, c.DistanceKm);

    public static EventResponse ToResponse(this Event e) =>
        new(
            e.EventId,
            e.OrganiserId,
            e.Organiser.FullName,
            e.Name,
            e.Description,
            e.EventDate,
            e.Location,
            e.DistanceKm,
            e.EventType,
            e.BannerImageUrl,
            e.Enrolments.Count(x => x.Status != EnrolmentStatus.Cancelled),
            e.Categories.OrderBy(c => c.Name).Select(c => c.ToResponse()).ToList());

    public static EnrolmentResponse ToResponse(this Enrolment en) =>
        new(
            en.EnrolmentId,
            en.ParticipantId,
            en.Participant.FullName,
            en.EventId,
            en.Event.Name,
            en.Event.EventDate,
            en.CategoryId,
            en.Category.Name,
            en.EnrolmentDate,
            en.Status);

    public static ResultResponse ToResponse(this Result r, int totalFinishers) =>
        new(
            r.ResultId,
            r.EnrolmentId,
            r.Enrolment.EventId,
            r.Enrolment.Event.Name,
            r.Enrolment.Event.EventDate,
            r.Enrolment.Participant.FullName,
            r.Enrolment.Category.Name,
            r.FinishTime,
            r.FinishPosition,
            totalFinishers);
}
