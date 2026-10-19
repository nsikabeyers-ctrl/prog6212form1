using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Tests.Infrastructure;

namespace RaceDay.Tests;

/// <summary>Participants entering events, and Organisers seeing who entered.</summary>
public class EnrolmentTests : ApiTestBase
{
    public EnrolmentTests(RaceDayFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Participant_Can_Enrol_And_The_Enrolment_Is_Recorded_In_The_Database()
    {
        var (organiser, _) = await LoggedInOrganiser();
        var (participant, participantId) = await LoggedInParticipant();
        var ev = await CreateEvent(organiser);
        var category = await CreateCategory(organiser, ev.EventId, "Jarred Senior");

        var response = await Post(participant, $"/api/events/{ev.EventId}/enrolments", new { categoryId = category.CategoryId });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await Read<EnrolmentResponse>(response);
        Assert.Equal(Api.Models.EnrolmentStatus.Pending, created!.Status);

        // The link between participant, event and category must really be saved.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var saved = await db.Enrolments.SingleAsync(en => en.EnrolmentId == created.EnrolmentId);
        Assert.Equal(participantId, saved.ParticipantId);
        Assert.Equal(ev.EventId, saved.EventId);
        Assert.Equal(category.CategoryId, saved.CategoryId);
    }

    [Fact]
    public async Task Enrolment_Shows_Up_In_The_Participants_Own_List()
    {
        var (organiser, _) = await LoggedInOrganiser();
        var (participant, _) = await LoggedInParticipant();
        var ev = await CreateEvent(organiser, "Jarred's Listed Race");
        var category = await CreateCategory(organiser, ev.EventId);
        var enrolment = await Enrol(participant, ev.EventId, category.CategoryId);

        var mine = await Read<List<EnrolmentResponse>>(await participant.GetAsync("/api/enrolments/mine"));

        var item = Assert.Single(mine!);
        Assert.Equal(enrolment.EnrolmentId, item.EnrolmentId);
        Assert.Equal("Jarred's Listed Race", item.EventName);
    }

    [Fact]
    public async Task Participant_Cannot_Enrol_Twice_In_The_Same_Event()
    {
        var (organiser, _) = await LoggedInOrganiser();
        var (participant, _) = await LoggedInParticipant();
        var ev = await CreateEvent(organiser);
        var category = await CreateCategory(organiser, ev.EventId);
        await Enrol(participant, ev.EventId, category.CategoryId);

        var second = await Post(participant, $"/api/events/{ev.EventId}/enrolments", new { categoryId = category.CategoryId });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Enrolling_With_A_Category_From_A_Different_Event_Is_Rejected()
    {
        var (organiser, _) = await LoggedInOrganiser();
        var (participant, _) = await LoggedInParticipant();
        var eventA = await CreateEvent(organiser, "Jarred's Race A");
        var eventB = await CreateEvent(organiser, "Jarred's Race B");
        var categoryOfB = await CreateCategory(organiser, eventB.EventId);

        var response = await Post(participant, $"/api/events/{eventA.EventId}/enrolments", new { categoryId = categoryOfB.CategoryId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Enrolling_In_An_Event_That_Does_Not_Exist_Gives_Not_Found()
    {
        var (participant, _) = await LoggedInParticipant();

        var response = await Post(participant, "/api/events/999999/enrolments", new { categoryId = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Enrolling_In_A_Category_Outside_Your_Age_Range_Is_Rejected()
    {
        var (organiser, _) = await LoggedInOrganiser();
        var (adult, _) = await LoggedInParticipant(dateOfBirth: "1990-01-01");
        var ev = await CreateEvent(organiser);
        var underTwenty = await CreateCategory(organiser, ev.EventId, "Under 20", minAge: 10, maxAge: 19);

        var response = await Post(adult, $"/api/events/{ev.EventId}/enrolments", new { categoryId = underTwenty.CategoryId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_Can_See_All_Enrolments_For_Their_Own_Event()
    {
        var (organiser, _) = await LoggedInOrganiser();
        var (jarred, _) = await LoggedInParticipant();
        var (friend, _) = await LoggedInParticipant();
        var ev = await CreateEvent(organiser);
        var category = await CreateCategory(organiser, ev.EventId);
        await Enrol(jarred, ev.EventId, category.CategoryId);
        await Enrol(friend, ev.EventId, category.CategoryId);

        var response = await organiser.GetAsync($"/api/events/{ev.EventId}/enrolments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await Read<List<EnrolmentResponse>>(response);
        Assert.Equal(2, list!.Count);
    }

    [Fact]
    public async Task Organiser_Cannot_See_Enrolments_For_Another_Organisers_Event()
    {
        var (owner, _) = await LoggedInOrganiser();
        var (otherOrganiser, _) = await LoggedInOrganiser();
        var ev = await CreateEvent(owner);

        var response = await otherOrganiser.GetAsync($"/api/events/{ev.EventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_Can_Confirm_An_Enrolment()
    {
        var (organiser, _) = await LoggedInOrganiser();
        var (participant, _) = await LoggedInParticipant();
        var ev = await CreateEvent(organiser);
        var category = await CreateCategory(organiser, ev.EventId);
        var enrolment = await Enrol(participant, ev.EventId, category.CategoryId);

        var response = await Put(organiser, $"/api/enrolments/{enrolment.EnrolmentId}/status", new { status = "Confirmed" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await Read<EnrolmentResponse>(response);
        Assert.Equal(Api.Models.EnrolmentStatus.Confirmed, updated!.Status);
    }

    [Fact]
    public async Task Participant_Can_Cancel_Their_Own_Enrolment()
    {
        var (organiser, _) = await LoggedInOrganiser();
        var (participant, _) = await LoggedInParticipant();
        var ev = await CreateEvent(organiser);
        var category = await CreateCategory(organiser, ev.EventId);
        var enrolment = await Enrol(participant, ev.EventId, category.CategoryId);

        var delete = await participant.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");
        var mine = await Read<List<EnrolmentResponse>>(await participant.GetAsync("/api/enrolments/mine"));

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Empty(mine!);
    }

    [Fact]
    public async Task Participant_Cannot_Cancel_Someone_Elses_Enrolment()
    {
        var (organiser, _) = await LoggedInOrganiser();
        var (jarred, _) = await LoggedInParticipant();
        var (stranger, _) = await LoggedInParticipant();
        var ev = await CreateEvent(organiser);
        var category = await CreateCategory(organiser, ev.EventId);
        var enrolment = await Enrol(jarred, ev.EventId, category.CategoryId);

        var response = await stranger.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
