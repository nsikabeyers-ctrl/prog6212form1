using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Security;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Organisers create, edit and delete their own events here.</summary>
[RoleRequired(Roles.Organiser)]
public class ManageEventsController : BaseController
{
    private const long MaxImageBytes = 2 * 1024 * 1024;
    private static readonly string[] AllowedImageTypes = { "image/jpeg", "image/png", "image/webp", "image/gif" };

    private readonly RaceDayApiClient _api;

    public ManageEventsController(RaceDayApiClient api)
    {
        _api = api;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var result = await _api.GetMyEventsAsync();
        if (SessionExpired(result) is { } expired) return expired;

        if (!result.Success)
        {
            Fail(result.Error ?? "Your events could not be loaded.");
            return View(new List<EventDto>());
        }

        return View(result.Data!.OrderBy(e => e.EventDate).ToList());
    }

    [HttpGet]
    public IActionResult Create() => View(new EventFormViewModel { EventDate = DateTime.Now.AddDays(30).Date.AddHours(7) });

    [HttpPost]
    [RequestSizeLimit(5_000_000)]
    public async Task<IActionResult> Create(EventFormViewModel model)
    {
        ValidateBanner(model);
        if (!ModelState.IsValid) return View(model);

        var created = await _api.CreateEventAsync(
            model.Name.Trim(),
            model.Description?.Trim(),
            model.EventDate!.Value,
            model.Location.Trim(),
            model.DistanceKm!.Value,
            model.EventType!.Value);

        if (SessionExpired(created) is { } expired) return expired;

        if (!created.Success)
        {
            AddApiErrors(created);
            return View(model);
        }

        // The banner goes to the API second, once the event has an id.
        if (model.Banner is { Length: > 0 })
        {
            var banner = await _api.UploadBannerAsync(created.Data!.EventId, model.Banner);
            if (!banner.Success)
            {
                Success("Your event was created.");
                Fail("The banner could not be uploaded: " + (banner.ValidationMessages.FirstOrDefault() ?? banner.Error)
                     + " You can add it again by editing the event.");
                return RedirectToAction(nameof(Index));
            }
        }

        Success("Your event was created. Add categories so people can enter.");
        return RedirectToAction("Index", "ManageCategories", new { eventId = created.Data!.EventId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var result = await _api.GetEventAsync(id);
        if (SessionExpired(result) is { } expired) return expired;
        if (result.IsNotFound) return NotFound();

        if (!result.Success)
        {
            Fail(result.Error ?? "That event could not be loaded.");
            return RedirectToAction(nameof(Index));
        }

        var ev = result.Data!;
        if (ev.OrganiserId != Me!.UserId) return RedirectToAction("AccessDenied", "Account");

        return View(new EventFormViewModel
        {
            EventId = ev.EventId,
            Name = ev.Name,
            Description = ev.Description,
            EventDate = ev.EventDate,
            Location = ev.Location,
            DistanceKm = ev.DistanceKm,
            EventType = ev.EventType,
            ExistingBannerUrl = ev.BannerImageUrl
        });
    }

    [HttpPost]
    [RequestSizeLimit(5_000_000)]
    public async Task<IActionResult> Edit(int id, EventFormViewModel model)
    {
        model.EventId = id;
        ValidateBanner(model);

        if (!ModelState.IsValid)
        {
            await KeepBanner(model);
            return View(model);
        }

        var updated = await _api.UpdateEventAsync(
            id,
            model.Name.Trim(),
            model.Description?.Trim(),
            model.EventDate!.Value,
            model.Location.Trim(),
            model.DistanceKm!.Value,
            model.EventType!.Value);

        if (SessionExpired(updated) is { } expired) return expired;

        if (updated.IsForbidden) return RedirectToAction("AccessDenied", "Account");
        if (updated.IsNotFound) return NotFound();

        if (!updated.Success)
        {
            AddApiErrors(updated);
            await KeepBanner(model);
            return View(model);
        }

        if (model.Banner is { Length: > 0 })
        {
            var banner = await _api.UploadBannerAsync(id, model.Banner);
            if (!banner.Success)
            {
                Success("Your changes were saved.");
                Fail("The banner could not be uploaded: " + (banner.ValidationMessages.FirstOrDefault() ?? banner.Error));
                return RedirectToAction(nameof(Index));
            }
        }

        Success("Your changes were saved.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _api.DeleteEventAsync(id);
        if (SessionExpired(result) is { } expired) return expired;

        if (result.Success)
        {
            Success("The event was deleted.");
        }
        else if (result.StatusCode == 409)
        {
            Fail("This event already has entries, so it cannot be deleted.");
        }
        else
        {
            Fail(result.Error ?? "The event could not be deleted.");
        }

        return RedirectToAction(nameof(Index));
    }

    private void ValidateBanner(EventFormViewModel model)
    {
        if (model.Banner is not { Length: > 0 }) return;

        if (model.Banner.Length > MaxImageBytes)
        {
            ModelState.AddModelError(nameof(model.Banner), "The banner must be 2 MB or smaller.");
        }
        else if (!AllowedImageTypes.Contains(model.Banner.ContentType))
        {
            ModelState.AddModelError(nameof(model.Banner), "Only JPG, PNG, WEBP or GIF images are allowed.");
        }
    }

    /// <summary>The form does not post the current banner address, so look it up again when the form is shown a second time.</summary>
    private async Task KeepBanner(EventFormViewModel model)
    {
        if (model.EventId is not int id) return;

        var current = await _api.GetEventAsync(id);
        if (current.Success) model.ExistingBannerUrl = current.Data!.BannerImageUrl;
    }
}
