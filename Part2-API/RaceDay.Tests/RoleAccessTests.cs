using System.Net;
using RaceDay.Tests.Infrastructure;

namespace RaceDay.Tests;

/// <summary>
/// The wrong role must be turned away (403) and a visitor who is not logged in must get 401.
/// Participants cannot do Organiser things, and Organisers cannot do Participant things.
/// </summary>
public class RoleAccessTests : ApiTestBase
{
    public RoleAccessTests(RaceDayFactory factory) : base(factory)
    {
    }

    // Participant trying Organiser functionality

    [Fact]
    public async Task Participant_Cannot_Create_An_Event()
    {
        var (participant, _) = await LoggedInParticipant();

        var response = await Post(participant, "/api/events", EventBody("Jarred Sneaks In"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_Cannot_Update_Or_Delete_An_Event()
    {
        var (organiser, _) = await LoggedInOrganiser();
        var (participant, _) = await LoggedInParticipant();
        var ev = await CreateEvent(organiser);

        var update = await Put(participant, $"/api/events/{ev.EventId}", EventBody("Hijacked"));
        var delete = await participant.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Participant_Cannot_Add_A_Category()
    {
        var (organiser, _) = await LoggedInOrganiser();
        var (participant, _) = await LoggedInParticipant();
        var ev = await CreateEvent(organiser);

        var response = await Post(participant, $"/api/events/{ev.EventId}/categories", new { name = "Jarred's Category" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_Cannot_View_All_Enrolments_For_An_Event()
    {
        var (organiser, _) = await LoggedInOrganiser();
        var (participant, _) = await LoggedInParticipant();
        var ev = await CreateEvent(organiser);

        var response = await participant.GetAsync($"/api/events/{ev.EventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_Cannot_Capture_A_Result()
    {
        var (organiser, _) = await LoggedInOrganiser();
        var (participant, _) = await LoggedInParticipant();
        var ev = await CreateEvent(organiser);
        var category = await CreateCategory(organiser, ev.EventId);
        var enrolment = await Enrol(participant, ev.EventId, category.CategoryId);

        var response = await Post(participant, $"/api/enrolments/{enrolment.EnrolmentId}/result",
            new { finishTime = "00:45:00", finishPosition = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_Cannot_Use_The_Organiser_Event_List()
    {
        var (participant, _) = await LoggedInParticipant();

        var response = await participant.GetAsync("/api/events/mine");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Organiser trying Participant functionality

    [Fact]
    public async Task Organiser_Cannot_Enrol_In_An_Event()
    {
        var (organiser, _) = await LoggedInOrganiser();
        var ev = await CreateEvent(organiser);
        var category = await CreateCategory(organiser, ev.EventId);

        var response = await Post(organiser, $"/api/events/{ev.EventId}/enrolments", new { categoryId = category.CategoryId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_Cannot_Use_Participant_Only_Lists()
    {
        var (organiser, _) = await LoggedInOrganiser();

        var enrolments = await organiser.GetAsync("/api/enrolments/mine");
        var results = await organiser.GetAsync("/api/results/mine");

        Assert.Equal(HttpStatusCode.Forbidden, enrolments.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, results.StatusCode);
    }

    // Not logged in at all

    [Fact]
    public async Task Visitor_Who_Is_Not_Logged_In_Gets_401_On_Organiser_Endpoints()
    {
        var visitor = NewClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(visitor, "/api/events", EventBody())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Put(visitor, "/api/events/1", EventBody())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.DeleteAsync("/api/events/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.GetAsync("/api/events/1/enrolments")).StatusCode);
    }

    [Fact]
    public async Task Visitor_Who_Is_Not_Logged_In_Gets_401_On_Participant_Endpoints()
    {
        var visitor = NewClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(visitor, "/api/events/1/enrolments", new { categoryId = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.GetAsync("/api/results/mine")).StatusCode);
    }
}
