using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Mapping;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

public class CategoriesController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public CategoriesController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>Lists the categories for an event.</summary>
    [HttpGet("api/events/{eventId:int}/categories")]
    [SessionAuthorize]
    [ProducesResponseType(typeof(List<CategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategories(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
        {
            return Error(StatusCodes.Status404NotFound, "Event not found");
        }

        var categories = await _db.Categories
            .AsNoTracking()
            .Where(c => c.EventId == eventId)
            .OrderBy(c => c.Name)
            .ToListAsync();

        return Ok(categories.Select(c => c.ToResponse()).ToList());
    }

    /// <summary>Adds an age or distance category to an event the Organiser owns.</summary>
    [HttpPost("api/events/{eventId:int}/categories")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateCategory(int eventId, CategoryRequest request)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return Error(StatusCodes.Status404NotFound, "Event not found");
        if (ev.OrganiserId != CurrentUserId)
        {
            return Error(StatusCodes.Status403Forbidden, "Not your event", "You can only add categories to events you created.");
        }

        if (request.MinAge is not null && request.MaxAge is not null && request.MinAge > request.MaxAge)
        {
            return Error(StatusCodes.Status400BadRequest, "Invalid age range", "MinAge cannot be higher than MaxAge.");
        }

        var category = new Category
        {
            EventId = eventId,
            Name = request.Name.Trim(),
            MinAge = request.MinAge,
            MaxAge = request.MaxAge,
            DistanceKm = request.DistanceKm
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return Created($"/api/events/{eventId}/categories", category.ToResponse());
    }

    /// <summary>Updates a category on an event the Organiser owns.</summary>
    [HttpPut("api/categories/{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCategory(int id, CategoryRequest request)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return Error(StatusCodes.Status404NotFound, "Category not found");
        if (category.Event.OrganiserId != CurrentUserId)
        {
            return Error(StatusCodes.Status403Forbidden, "Not your event", "You can only change categories on events you created.");
        }

        if (request.MinAge is not null && request.MaxAge is not null && request.MinAge > request.MaxAge)
        {
            return Error(StatusCodes.Status400BadRequest, "Invalid age range", "MinAge cannot be higher than MaxAge.");
        }

        category.Name = request.Name.Trim();
        category.MinAge = request.MinAge;
        category.MaxAge = request.MaxAge;
        category.DistanceKm = request.DistanceKm;

        await _db.SaveChangesAsync();
        return Ok(category.ToResponse());
    }

    /// <summary>Removes a category. Blocked if participants have already enrolled in it.</summary>
    [HttpDelete("api/categories/{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return Error(StatusCodes.Status404NotFound, "Category not found");
        if (category.Event.OrganiserId != CurrentUserId)
        {
            return Error(StatusCodes.Status403Forbidden, "Not your event", "You can only delete categories on events you created.");
        }

        if (await _db.Enrolments.AnyAsync(en => en.CategoryId == id))
        {
            return Error(StatusCodes.Status409Conflict, "Category in use", "Participants have enrolled in this category.");
        }

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
