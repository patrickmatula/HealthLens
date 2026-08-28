using HealthLens.Api.Data;
using HealthLens.Api.Dtos;
using HealthLens.Api.Models;
using HealthLens.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthLens.Api.Controllers;

/// <summary>
/// Optional shoe-mileage tracking: create shoes, assign them to workouts one at a time or in bulk
/// (the frontend drives "bulk" off the existing Workouts search/filter selection — this controller
/// just takes whatever set of workout ids it's given). Entirely opt-in on the frontend; nothing here
/// requires the feature to be "enabled" since an unused Shoes table costs nothing.
/// </summary>
[ApiController]
[Route("api/shoes")]
public class ShoesController(DataSessionService session, ShoeDefaultsStore shoeDefaults) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ShoeDto>>> List(CancellationToken ct)
    {
        await using var db = session.CreateContext();

        var shoes = await db.Shoes.OrderBy(s => s.IsRetired).ThenByDescending(s => s.CreatedAtUtc).ToListAsync(ct);
        var stats = await db.Workouts
            .Where(w => w.ShoeId != null)
            .GroupBy(w => w.ShoeId!.Value)
            .Select(g => new { ShoeId = g.Key, Count = g.Count(), Distance = g.Sum(w => w.DistanceMeters ?? 0) })
            .ToDictionaryAsync(g => g.ShoeId, ct);

        return Ok(shoes.Select(s =>
        {
            stats.TryGetValue(s.Id, out var stat);
            return new ShoeDto(s.Id, s.Name, s.Brand, s.IsRetired, s.CreatedAtUtc, stat?.Count ?? 0, stat?.Distance ?? 0);
        }).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<ShoeDto>> Create(CreateShoeDto dto, CancellationToken ct)
    {
        var name = dto.Name.Trim();
        if (name.Length == 0)
        {
            return BadRequest("Name darf nicht leer sein.");
        }

        await using var db = session.CreateContext();
        var shoe = new Shoe { Name = name, Brand = string.IsNullOrWhiteSpace(dto.Brand) ? null : dto.Brand.Trim(), CreatedAtUtc = DateTime.UtcNow };
        db.Shoes.Add(shoe);
        await db.SaveChangesAsync(ct);

        return Ok(new ShoeDto(shoe.Id, shoe.Name, shoe.Brand, shoe.IsRetired, shoe.CreatedAtUtc, 0, 0));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ShoeDto>> Update(int id, UpdateShoeDto dto, CancellationToken ct)
    {
        var name = dto.Name.Trim();
        if (name.Length == 0)
        {
            return BadRequest("Name darf nicht leer sein.");
        }

        await using var db = session.CreateContext();
        var shoe = await db.Shoes.FindAsync([id], ct);
        if (shoe is null)
        {
            return NotFound();
        }

        shoe.Name = name;
        shoe.Brand = string.IsNullOrWhiteSpace(dto.Brand) ? null : dto.Brand.Trim();
        shoe.IsRetired = dto.IsRetired;
        await db.SaveChangesAsync(ct);

        var stat = await db.Workouts.Where(w => w.ShoeId == id).GroupBy(w => 1).Select(g => new { Count = g.Count(), Distance = g.Sum(w => w.DistanceMeters ?? 0) }).FirstOrDefaultAsync(ct);
        return Ok(new ShoeDto(shoe.Id, shoe.Name, shoe.Brand, shoe.IsRetired, shoe.CreatedAtUtc, stat?.Count ?? 0, stat?.Distance ?? 0));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await using var db = session.CreateContext();
        if (!await db.Shoes.AnyAsync(s => s.Id == id, ct))
        {
            return NotFound();
        }

        // SQLite FK enforcement isn't turned on for this app's connections, so the ON DELETE SET NULL
        // mapping in AppDbContext is documentation, not something to rely on — clear assignments explicitly.
        await db.Workouts.Where(w => w.ShoeId == id).ExecuteUpdateAsync(s => s.SetProperty(w => w.ShoeId, (int?)null), ct);
        await db.Shoes.Where(s => s.Id == id).ExecuteDeleteAsync(ct);
        return NoContent();
    }

    /// <summary>The per-category default shoe ("every new run gets Shoe X"), applied automatically to
    /// genuinely new workouts by the Takeout importer and Google Health sync — see ShoeDefaultsStore.</summary>
    [HttpGet("defaults")]
    public ActionResult<IReadOnlyList<ShoeDefaultDto>> GetDefaults()
    {
        var defaults = shoeDefaults.Load();
        return Ok(WorkoutCategorizer.Categories.Select(c => new ShoeDefaultDto(c, defaults.TryGetValue(c, out var id) ? id : null)).ToList());
    }

    [HttpPut("defaults/{category}")]
    public async Task<ActionResult<IReadOnlyList<ShoeDefaultDto>>> SetDefault(string category, SetShoeDefaultDto dto, CancellationToken ct)
    {
        if (!WorkoutCategorizer.Categories.Contains(category))
        {
            return BadRequest("Unbekannte Workout-Art.");
        }

        await using var db = session.CreateContext();
        if (dto.ShoeId is { } shoeId && !await db.Shoes.AnyAsync(s => s.Id == shoeId, ct))
        {
            return NotFound("Schuh nicht gefunden.");
        }

        var defaults = shoeDefaults.Load();
        if (dto.ShoeId is null)
        {
            defaults.Remove(category);
        }
        else
        {
            defaults[category] = dto.ShoeId.Value;
        }

        shoeDefaults.Save(defaults);
        return GetDefaults();
    }

    /// <summary>Assigns (or, with ShoeId null, clears) a shoe across any set of workout ids — the frontend's "bulk assign" is just this called with every currently filtered/selected workout.</summary>
    [HttpPost("assign")]
    public async Task<IActionResult> Assign(AssignShoeDto dto, CancellationToken ct)
    {
        var ids = new List<long>(dto.WorkoutIds.Count);
        foreach (var raw in dto.WorkoutIds)
        {
            if (!long.TryParse(raw, out var id))
            {
                return BadRequest($"Ungültige Workout-Id: {raw}");
            }

            ids.Add(id);
        }

        if (ids.Count == 0)
        {
            return BadRequest("Keine Workouts ausgewählt.");
        }

        await using var db = session.CreateContext();
        if (dto.ShoeId is { } shoeId && !await db.Shoes.AnyAsync(s => s.Id == shoeId, ct))
        {
            return NotFound("Schuh nicht gefunden.");
        }

        await db.Workouts.Where(w => ids.Contains(w.Id)).ExecuteUpdateAsync(s => s.SetProperty(w => w.ShoeId, dto.ShoeId), ct);
        return NoContent();
    }
}
