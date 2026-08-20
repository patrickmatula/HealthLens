using HealthLens.Api.Data;
using HealthLens.Api.Dtos;
using HealthLens.Api.Models;
using HealthLens.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthLens.Api.Controllers;

/// <summary>
/// Optional body-measurement tracking. The frontend decides which measurement types to show entry
/// fields/charts for and computes BMI/waist-to-hip-ratio/body-fat-% assessments client-side (same
/// split as the resting-heart-rate/running-dynamics reference gauges) — this controller just stores
/// and returns raw dated values plus the height/sex profile those computations need.
/// </summary>
[ApiController]
[Route("api/body")]
public class BodyController(DataSessionService session) : ControllerBase
{
    [HttpGet("profile")]
    public async Task<ActionResult<BodyProfileDto>> GetProfile(CancellationToken ct)
    {
        await using var db = session.CreateContext();
        var profile = await db.BodyProfiles.FindAsync([1], ct);
        return Ok(new BodyProfileDto(profile?.HeightCm, profile?.Sex));
    }

    [HttpPut("profile")]
    public async Task<ActionResult<BodyProfileDto>> UpdateProfile(BodyProfileDto dto, CancellationToken ct)
    {
        await using var db = session.CreateContext();
        var profile = await db.BodyProfiles.FindAsync([1], ct);
        if (profile is null)
        {
            profile = new BodyProfile { Id = 1 };
            db.BodyProfiles.Add(profile);
        }

        profile.HeightCm = dto.HeightCm;
        profile.Sex = dto.Sex;
        await db.SaveChangesAsync(ct);

        return Ok(new BodyProfileDto(profile.HeightCm, profile.Sex));
    }

    [HttpGet("overview")]
    public async Task<ActionResult<BodyOverviewDto>> Overview(string? preset, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        await using var db = session.CreateContext();
        var profile = await db.BodyProfiles.FindAsync([1], ct);
        var profileDto = new BodyProfileDto(profile?.HeightCm, profile?.Sex);

        if (!await db.BodyMeasurements.AnyAsync(ct))
        {
            return Ok(new BodyOverviewDto(profileDto, []));
        }

        var earliest = await db.BodyMeasurements.MinAsync(m => m.Date, ct);
        var latest = await db.BodyMeasurements.MaxAsync(m => m.Date, ct);
        var range = TimeRange.Resolve(preset, from, to, earliest, latest);

        var measurements = await db.BodyMeasurements
            .Where(m => m.Date >= range.From && m.Date <= range.To)
            .OrderBy(m => m.Date)
            .ToListAsync(ct);

        return Ok(new BodyOverviewDto(profileDto, measurements.Select(m => new BodyMeasurementPointDto(m.Date, m.Type.ToString(), m.Value)).ToList()));
    }

    /// <summary>Upserts one day's worth of entries — a re-submitted date overwrites, so correcting a mistake is just entering it again.</summary>
    [HttpPost("entry")]
    public async Task<IActionResult> SubmitEntry(BodyEntryDto dto, CancellationToken ct)
    {
        await using var db = session.CreateContext();

        foreach (var (typeName, value) in dto.Values)
        {
            if (!Enum.TryParse<BodyMeasurementType>(typeName, out var type))
            {
                return BadRequest($"Unbekannter Messwert-Typ: {typeName}");
            }

            var existing = await db.BodyMeasurements.FindAsync([dto.Date, type], ct);
            if (existing is null)
            {
                db.BodyMeasurements.Add(new BodyMeasurement { Date = dto.Date, Type = type, Value = value });
            }
            else
            {
                existing.Value = value;
            }
        }

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("entry/{date}")]
    public async Task<IActionResult> DeleteEntry(DateOnly date, CancellationToken ct)
    {
        await using var db = session.CreateContext();
        await db.BodyMeasurements.Where(m => m.Date == date).ExecuteDeleteAsync(ct);
        return NoContent();
    }
}
