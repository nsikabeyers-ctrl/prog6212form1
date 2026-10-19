using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.DTOs;

public class RegisterRequest
{
    [Required, StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).{8,100}$",
        ErrorMessage = "Password must be 8 to 100 characters and contain at least one letter and one number.")]
    public string Password { get; set; } = string.Empty;

    /// <summary>Either "Organiser" or "Participant".</summary>
    [Required]
    public string Role { get; set; } = string.Empty;

    /// <summary>Participants only. Used to check age categories when enrolling.</summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>Organisers only.</summary>
    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    /// <summary>Required when registering as an Organiser. Stops anyone from simply picking the Organiser role.</summary>
    public string? OrganiserInviteCode { get; set; }
}

public class LoginRequest
{
    [Required]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class UpdateProfileRequest
{
    [Required, StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    public DateOnly? DateOfBirth { get; set; }
}

public class EventRequest
{
    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    public DateTime? EventDate { get; set; }

    [Required, StringLength(200)]
    public string Location { get; set; } = string.Empty;

    [Required, Range(0.1, 1000)]
    public decimal? DistanceKm { get; set; }

    /// <summary>Run, Walk or Cycle.</summary>
    [Required]
    public EventType? EventType { get; set; }
}

public class CategoryRequest
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 120)]
    public int? MinAge { get; set; }

    [Range(0, 120)]
    public int? MaxAge { get; set; }

    [Range(0.1, 1000)]
    public decimal? DistanceKm { get; set; }
}

public class EnrolRequest
{
    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }
}

public class EnrolmentStatusRequest
{
    /// <summary>Pending, Confirmed or Cancelled.</summary>
    [Required]
    public EnrolmentStatus? Status { get; set; }
}

public class ResultRequest
{
    /// <summary>Finish time as hh:mm:ss, for example 00:48:32.</summary>
    [Required]
    public TimeSpan? FinishTime { get; set; }

    [Required, Range(1, 1000000)]
    public int? FinishPosition { get; set; }
}
