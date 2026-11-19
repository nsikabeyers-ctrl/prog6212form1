using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Security;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>Both roles can see and change their own details. Only Participants have a profile picture.</summary>
[RoleRequired]
public class ProfileController : BaseController
{
    private const long MaxImageBytes = 2 * 1024 * 1024;
    private static readonly string[] AllowedImageTypes = { "image/jpeg", "image/png", "image/webp", "image/gif" };

    private readonly RaceDayApiClient _api;

    public ProfileController(RaceDayApiClient api)
    {
        _api = api;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var result = await _api.GetProfileAsync();
        if (SessionExpired(result) is { } expired) return expired;

        if (!result.Success)
        {
            Fail(result.Error ?? "Your profile could not be loaded.");
            return RedirectToAction("Index", "Home");
        }

        return View(ToViewModel(result.Data!));
    }

    [HttpPost]
    public async Task<IActionResult> Update(ProfileViewModel model)
    {
        if (!ModelState.IsValid) return View(nameof(Index), await Refill(model));

        var user = Me!;
        var result = await _api.UpdateProfileAsync(
            model.FullName.Trim(),
            user.IsOrganiser ? model.PhoneNumber?.Trim() : null,
            user.IsParticipant ? model.DateOfBirth : null);

        if (SessionExpired(result) is { } expired) return expired;

        if (!result.Success)
        {
            AddApiErrors(result);
            return View(nameof(Index), await Refill(model));
        }

        // Keep the name in the navigation bar up to date.
        HttpContext.Session.SetString(SessionKeys.FullName, result.Data!.FullName);

        Success("Your profile has been updated.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [RoleRequired(Roles.Participant)]
    [RequestSizeLimit(5_000_000)]
    public async Task<IActionResult> Picture(IFormFile? picture)
    {
        if (picture is null || picture.Length == 0)
        {
            Fail("Choose an image before you upload.");
            return RedirectToAction(nameof(Index));
        }

        if (picture.Length > MaxImageBytes)
        {
            Fail("The image must be 2 MB or smaller.");
            return RedirectToAction(nameof(Index));
        }

        if (!AllowedImageTypes.Contains(picture.ContentType))
        {
            Fail("Only JPG, PNG, WEBP or GIF images are allowed.");
            return RedirectToAction(nameof(Index));
        }

        // The file goes to the API. The API stores it in Blob Storage and saves the URL.
        var result = await _api.UploadProfilePictureAsync(picture);
        if (SessionExpired(result) is { } expired) return expired;

        if (result.Success)
        {
            Success("Your profile picture has been updated.");
        }
        else
        {
            Fail(result.ValidationMessages.FirstOrDefault() ?? result.Error ?? "The picture could not be uploaded.");
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>The form does not post the email or picture, so fetch them again when the form has to be shown a second time.</summary>
    private async Task<ProfileViewModel> Refill(ProfileViewModel model)
    {
        model.Role = Me!.Role;

        var current = await _api.GetProfileAsync();
        if (current.Success)
        {
            model.Email = current.Data!.Email;
            model.ProfilePictureUrl = current.Data.ProfilePictureUrl;
        }

        return model;
    }

    private static ProfileViewModel ToViewModel(ProfileDto profile) => new()
    {
        Role = profile.Role,
        FullName = profile.FullName,
        Email = profile.Email,
        PhoneNumber = profile.PhoneNumber,
        DateOfBirth = profile.DateOfBirth,
        ProfilePictureUrl = profile.ProfilePictureUrl
    };
}
