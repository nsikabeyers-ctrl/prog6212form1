using System.Net;
using RaceDay.Api.DTOs;
using RaceDay.Tests.Infrastructure;

namespace RaceDay.Tests;

/// <summary>Organisers capturing finish times and Participants reading their own race history.</summary>
public class ResultTests : ApiTestBase
{
    public ResultTests(RaceDayFactory factory) : base(factory)
    {
    }

    private async Task<(HttpClient Organiser, HttpClient Participant, EventResponse Event, EnrolmentResponse Enrolment)> SetUpEnrolment()
    {
        var (organiser, _) = await LoggedInOrganiser();
        var (participant, _) = await LoggedInParticipant();
        var ev = await CreateEvent(organiser, "Jarred's Timed 10km");
        var category = await CreateCategory(organiser, ev.EventId);
        var enrolment = await Enrol(participant, ev.EventId, category.CategoryId);
        return (organiser, participant, ev, enrolment);
    }

    [Fact]
    public async Task Organiser_Can_Capture_A_Result()
    {
        var (organiser, _, _, enrolment) = await SetUpEnrolment();

        var response = await Post(organiser, $"/api/enrolments/{enrolment.EnrolmentId}/result",
            new { finishTime = "00:48:32", finishPosition = 12 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await Read<ResultResponse>(response);
        Assert.Equal(new TimeSpan(0, 48, 32), result!.FinishTime);
        Assert.Equal(12, result.FinishPosition);
    }

    [Fact]
    public async Task Participant_Sees_Their_Own_Result_In_My_Results()
    {
        var (organiser, participant, ev, enrolment) = await SetUpEnrolment();
        await Post(organiser, $"/api/enrolments/{enrolment.EnrolmentId}/result",
            new { finishTime = "00:50:00", finishPosition = 3 });

        var mine = await Read<List<ResultResponse>>(await participant.GetAsync("/api/results/mine"));

        var item = Assert.Single(mine!);
        Assert.Equal(ev.EventId, item.EventId);
        Assert.Equal("Jarred's Timed 10km", item.EventName);
        Assert.Equal(3, item.FinishPosition);
        Assert.Equal(1, item.TotalFinishers);
    }

    [Fact]
    public async Task Participant_Does_Not_See_Another_Participants_Result()
    {
        var (organiser, _, _, enrolment) = await SetUpEnrolment();
        await Post(organiser, $"/api/enrolments/{enrolment.EnrolmentId}/result",
            new { finishTime = "00:50:00", finishPosition = 3 });
        var (stranger, _) = await LoggedInParticipant();

        var mine = await Read<List<ResultResponse>>(await stranger.GetAsync("/api/results/mine"));

        Assert.Empty(mine!);
    }

    [Fact]
    public async Task Capturing_A_Second_Result_For_The_Same_Enrolment_Gives_Conflict()
    {
        var (organiser, _, _, enrolment) = await SetUpEnrolment();
        var url = $"/api/enrolments/{enrolment.EnrolmentId}/result";
        await Post(organiser, url, new { finishTime = "00:48:32", finishPosition = 12 });

        var second = await Post(organiser, url, new { finishTime = "00:40:00", finishPosition = 1 });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Organiser_Can_Correct_A_Result()
    {
        var (organiser, _, _, enrolment) = await SetUpEnrolment();
        var created = await Read<ResultResponse>(await Post(organiser, $"/api/enrolments/{enrolment.EnrolmentId}/result",
            new { finishTime = "00:48:32", finishPosition = 12 }));

        var response = await Put(organiser, $"/api/results/{created!.ResultId}", new { finishTime = "00:47:59", finishPosition = 11 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await Read<ResultResponse>(response);
        Assert.Equal(11, updated!.FinishPosition);
    }

    [Fact]
    public async Task Result_With_An_Invalid_Position_Is_Rejected()
    {
        var (organiser, _, _, enrolment) = await SetUpEnrolment();

        var response = await Post(organiser, $"/api/enrolments/{enrolment.EnrolmentId}/result",
            new { finishTime = "00:48:32", finishPosition = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_Cannot_Capture_A_Result_For_Another_Organisers_Event()
    {
        var (_, _, _, enrolment) = await SetUpEnrolment();
        var (otherOrganiser, _) = await LoggedInOrganiser();

        var response = await Post(otherOrganiser, $"/api/enrolments/{enrolment.EnrolmentId}/result",
            new { finishTime = "00:48:32", finishPosition = 12 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Results_For_An_Event_Come_Back_In_Finishing_Order()
    {
        var (organiser, _) = await LoggedInOrganiser();
        var ev = await CreateEvent(organiser, "Jarred's Ranked Race");
        var category = await CreateCategory(organiser, ev.EventId);

        var (slow, _) = await LoggedInParticipant();
        var (fast, _) = await LoggedInParticipant();
        var slowEnrolment = await Enrol(slow, ev.EventId, category.CategoryId);
        var fastEnrolment = await Enrol(fast, ev.EventId, category.CategoryId);

        await Post(organiser, $"/api/enrolments/{slowEnrolment.EnrolmentId}/result", new { finishTime = "01:10:00", finishPosition = 2 });
        await Post(organiser, $"/api/enrolments/{fastEnrolment.EnrolmentId}/result", new { finishTime = "00:45:00", finishPosition = 1 });

        var results = await Read<List<ResultResponse>>(await organiser.GetAsync($"/api/events/{ev.EventId}/results"));

        Assert.Equal(new[] { 1, 2 }, results!.Select(r => r.FinishPosition).ToArray());
        Assert.All(results!, r => Assert.Equal(2, r.TotalFinishers));
    }
}
