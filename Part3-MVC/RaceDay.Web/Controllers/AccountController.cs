using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Security;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class AccountController : BaseController
{
    private readonly RaceDayApiClient _api;

    public AccountController(RaceDayApiClient api)
    {
        _api = api;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (Me is { } user) return HomeFor(user);

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _api.LoginAsync(model.Email.Trim(), model.Password);
        if (!result.Success)
        {
            AddApiErrors(result);
            model.Password = string.Empty;
            return View(model);
        }

        HttpContext.Session.SignIn(result.Data!);
        Success($"Welcome back, {FirstName(result.Data!.FullName)}.");

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return LocalRedirect(model.ReturnUrl);
        }

        return HomeFor(Me!);
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (Me is { } user) return HomeFor(user);

        return View(new RegisterViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        var isOrganiser = model.Role == Roles.Organiser;
        var isParticipant = model.Role == Roles.Participant;

        if (!isOrganiser && !isParticipant)
        {
            ModelState.AddModelError(nameof(model.Role), "Choose Organiser or Participant.");
        }

        if (isOrganiser && string.IsNullOrWhiteSpace(model.OrganiserInviteCode))
        {
            ModelState.AddModelError(nameof(model.OrganiserInviteCode), "Organisers need an invite code.");
        }

        if (isParticipant && model.DateOfBirth is null)
        {
            ModelState.AddModelError(nameof(model.DateOfBirth), "Enter your date of birth so we can match you to age categories.");
        }

        if (!ModelState.IsValid) return View(model);

        var registered = await _api.RegisterAsync(
            model.FullName.Trim(),
            model.Email.Trim(),
            model.Password,
            model.Role,
            isParticipant ? model.DateOfBirth : null,
            isOrganiser ? model.PhoneNumber?.Trim() : null,
            isOrganiser ? model.OrganiserInviteCode?.Trim() : null);

        if (!registered.Success)
        {
            AddApiErrors(registered);
            return View(model);
        }

        // Log them straight in so they do not have to type it all again.
        var login = await _api.LoginAsync(model.Email.Trim(), model.Password);
        if (!login.Success)
        {
            Success("Your account is ready. Please log in.");
            return RedirectToAction(nameof(Login));
        }

        HttpContext.Session.SignIn(login.Data!);
        Success($"Welcome to RaceDay, {FirstName(login.Data!.FullName)}.");
        return HomeFor(Me!);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        // Ask the API to end its session too. If that fails we still clear ours.
        await _api.LogoutAsync();
        HttpContext.Session.Clear();

        Success("You have been logged out.");
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    private static string FirstName(string fullName) =>
        fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? fullName;
}
