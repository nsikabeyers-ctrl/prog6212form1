using System.ComponentModel.DataAnnotations;
using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Models;

// ---------- Account ----------

public class LoginViewModel
{
    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "That does not look like an email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your password.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Enter your full name.")]
    [StringLength(150)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "That does not look like an email address.")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose a password.")]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).{8,100}$",
        ErrorMessage = "Use 8 to 100 characters with at least one letter and one number.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Type the password again.")]
    [Compare(nameof(Password), ErrorMessage = "The two passwords do not match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Choose how you will use RaceDay.")]
    public string Role { get; set; } = "Participant";

    [Display(Name = "Date of birth")]
    public DateOnly? DateOfBirth { get; set; }

    [StringLength(20)]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Organiser invite code")]
    public string? OrganiserInviteCode { get; set; }
}

// ---------- Participant ----------

public class EventListViewModel
{
    public List<EventDto> Events { get; set; } = new();
    public HashSet<int> EnteredEventIds { get; set; } = new();

    public string? Type { get; set; }
    public string? Search { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(Type) || !string.IsNullOrWhiteSpace(Search) || From is not null || To is not null;
}

public class EventDetailsViewModel
{
    public EventDto Event { get; set; } = null!;
    public EnrolmentDto? MyEnrolment { get; set; }
    public bool HasPassed { get; set; }
}

public class ProfileViewModel
{
    public string Role { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your full name.")]
    [StringLength(150)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    [StringLength(20)]
    [Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Date of birth")]
    public DateOnly? DateOfBirth { get; set; }

    public string? ProfilePictureUrl { get; set; }
}

// ---------- Organiser ----------

public class DashboardViewModel
{
    public List<EventDto> Events { get; set; } = new();
    public int TotalEvents => Events.Count;
    public int UpcomingEvents => Events.Count(e => e.EventDate > DateTime.UtcNow);
    public int TotalEnrolments => Events.Sum(e => e.EnrolmentCount);
    public EventDto? NextEvent => Events.Where(e => e.EventDate > DateTime.UtcNow).OrderBy(e => e.EventDate).FirstOrDefault();
}

public class EventFormViewModel
{
    public int? EventId { get; set; }

    [Required(ErrorMessage = "Give the event a name.")]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Choose the date and start time.")]
    [Display(Name = "Date and start time")]
    public DateTime? EventDate { get; set; }

    [Required(ErrorMessage = "Enter the location.")]
    [StringLength(200)]
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the distance in kilometres.")]
    [Range(0.1, 1000, ErrorMessage = "Distance must be between 0.1 and 1000 km.")]
    [Display(Name = "Distance (km)")]
    public decimal? DistanceKm { get; set; }

    [Required(ErrorMessage = "Choose run, walk or cycle.")]
    [Display(Name = "Event type")]
    public EventType? EventType { get; set; }

    [Display(Name = "Banner image")]
    public IFormFile? Banner { get; set; }

    public string? ExistingBannerUrl { get; set; }
}

public class CategoryFormViewModel
{
    public int? CategoryId { get; set; }
    public int EventId { get; set; }

    [Required(ErrorMessage = "Give the category a name.")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 120, ErrorMessage = "Minimum age must be between 0 and 120.")]
    [Display(Name = "Minimum age")]
    public int? MinAge { get; set; }

    [Range(0, 120, ErrorMessage = "Maximum age must be between 0 and 120.")]
    [Display(Name = "Maximum age")]
    public int? MaxAge { get; set; }

    [Range(0.1, 1000, ErrorMessage = "Distance must be between 0.1 and 1000 km.")]
    [Display(Name = "Distance (km)")]
    public decimal? DistanceKm { get; set; }
}

public class ManageCategoriesViewModel
{
    public EventDto Event { get; set; } = null!;
    public CategoryFormViewModel NewCategory { get; set; } = new();
}

public class ManageEnrolmentsViewModel
{
    public EventDto Event { get; set; } = null!;
    public List<EnrolmentDto> Enrolments { get; set; } = new();
}

public class ResultRow
{
    public EnrolmentDto Enrolment { get; set; } = null!;
    public ResultDto? Result { get; set; }
}

public class ManageResultsViewModel
{
    public EventDto Event { get; set; } = null!;
    public List<ResultRow> Rows { get; set; } = new();
    public bool HasPassed { get; set; }
}

// ---------- Shared pieces ----------

public record EventCardModel(EventDto Event, bool Entered);
