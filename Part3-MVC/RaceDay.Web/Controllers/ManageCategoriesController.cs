using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Security;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Organisers add the age and distance categories for their events.</summary>
[RoleRequired(Roles.Organiser)]
public class ManageCategoriesController : BaseController
{
    private readonly RaceDayApiClient _api;

    public ManageCategoriesController(RaceDayApiClient api)
    {
        _api = api;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int eventId)
    {
        var ev = await LoadOwnEvent(eventId);
        if (ev.Redirect is not null) return ev.Redirect;

        return View(new ManageCategoriesViewModel
        {
            Event = ev.Event!,
            NewCategory = new CategoryFormViewModel { EventId = eventId }
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CategoryFormViewModel model)
    {
        CheckAges(model);

        if (!ModelState.IsValid)
        {
            // Show the page again with the problems next to the form.
            var ev = await LoadOwnEvent(model.EventId);
            if (ev.Redirect is not null) return ev.Redirect;

            return View(nameof(Index), new ManageCategoriesViewModel { Event = ev.Event!, NewCategory = model });
        }

        var result = await _api.CreateCategoryAsync(model.EventId, model.Name.Trim(), model.MinAge, model.MaxAge, model.DistanceKm);
        if (SessionExpired(result) is { } expired) return expired;

        if (!result.Success)
        {
            AddApiErrors(result);

            var ev = await LoadOwnEvent(model.EventId);
            if (ev.Redirect is not null) return ev.Redirect;

            return View(nameof(Index), new ManageCategoriesViewModel { Event = ev.Event!, NewCategory = model });
        }

        Success("The category was added.");
        return RedirectToAction(nameof(Index), new { eventId = model.EventId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, int eventId)
    {
        var ev = await LoadOwnEvent(eventId);
        if (ev.Redirect is not null) return ev.Redirect;

        var category = ev.Event!.Categories.FirstOrDefault(c => c.CategoryId == id);
        if (category is null) return NotFound();

        ViewData["EventName"] = ev.Event.Name;
        return View(new CategoryFormViewModel
        {
            CategoryId = category.CategoryId,
            EventId = eventId,
            Name = category.Name,
            MinAge = category.MinAge,
            MaxAge = category.MaxAge,
            DistanceKm = category.DistanceKm
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, CategoryFormViewModel model)
    {
        model.CategoryId = id;
        CheckAges(model);

        if (!ModelState.IsValid) return await EditAgain(model);

        var result = await _api.UpdateCategoryAsync(id, model.Name.Trim(), model.MinAge, model.MaxAge, model.DistanceKm);
        if (SessionExpired(result) is { } expired) return expired;

        if (result.IsForbidden) return RedirectToAction("AccessDenied", "Account");
        if (result.IsNotFound) return NotFound();

        if (!result.Success)
        {
            AddApiErrors(result);
            return await EditAgain(model);
        }

        Success("The category was updated.");
        return RedirectToAction(nameof(Index), new { eventId = model.EventId });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, int eventId)
    {
        var result = await _api.DeleteCategoryAsync(id);
        if (SessionExpired(result) is { } expired) return expired;

        if (result.Success)
        {
            Success("The category was removed.");
        }
        else if (result.StatusCode == 409)
        {
            Fail("People have already entered under this category, so it cannot be removed.");
        }
        else
        {
            Fail(result.Error ?? "The category could not be removed.");
        }

        return RedirectToAction(nameof(Index), new { eventId });
    }

    private void CheckAges(CategoryFormViewModel model)
    {
        if (model.MinAge is int min && model.MaxAge is int max && min > max)
        {
            ModelState.AddModelError(nameof(model.MinAge), "The minimum age cannot be higher than the maximum age.");
        }
    }

    private async Task<IActionResult> EditAgain(CategoryFormViewModel model)
    {
        var ev = await LoadOwnEvent(model.EventId);
        if (ev.Redirect is not null) return ev.Redirect;

        ViewData["EventName"] = ev.Event!.Name;
        return View(model);
    }

    /// <summary>Loads the event and makes sure it belongs to the logged-in Organiser.</summary>
    private async Task<(EventDto? Event, IActionResult? Redirect)> LoadOwnEvent(int eventId)
    {
        var result = await _api.GetEventAsync(eventId);
        if (SessionExpired(result) is { } expired) return (null, expired);
        if (result.IsNotFound) return (null, NotFound());

        if (!result.Success)
        {
            Fail(result.Error ?? "That event could not be loaded.");
            return (null, RedirectToAction("Index", "ManageEvents"));
        }

        if (result.Data!.OrganiserId != Me!.UserId)
        {
            return (null, RedirectToAction("AccessDenied", "Account"));
        }

        return (result.Data, null);
    }
}
