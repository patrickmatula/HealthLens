using HealthLens.Api.Data;
using HealthLens.Api.Dtos;
using HealthLens.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthLens.Api.Controllers;

[ApiController]
[Route("api/sleep")]
public class SleepController(DataSessionService session) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SleepSummaryDto>> Summary(string? preset, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        await using var db = session.CreateContext();

        if (!await db.SleepSessions.AnyAsync(ct))
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            return Ok(new SleepSummaryDto(today, today, 0, 0, 0, 0, null, []));
        }

        var earliest = DateOnly.FromDateTime(await db.SleepSessions.MinAsync(s => s.StartUtc, ct));
        var latest = DateOnly.FromDateTime(await db.SleepSessions.MaxAsync(s => s.StartUtc, ct));
        var range = TimeRange.Resolve(preset, from, to, earliest, latest);

        var sessions = await db.SleepSessions
            .Where(s => s.StartUtc >= range.StartUtc && s.StartUtc <= range.EndUtc)
            .Include(s => s.Score)
            .OrderByDescending(s => s.StartUtc)
            .ToListAsync(ct);

        var scored = sessions.Where(s => s.Score is not null).ToList();
        var withEfficiency = sessions.Where(s => s.TimeInBedMinutes > 0).ToList();

        return Ok(new SleepSummaryDto(
            range.From,
            range.To,
            sessions.Count,
            sessions.Count > 0 ? sessions.Average(s => s.MinutesAsleep) : 0,
            sessions.Count > 0 ? sessions.Average(s => s.TimeInBedMinutes) : 0,
            withEfficiency.Count > 0 ? withEfficiency.Average(s => 100.0 * s.MinutesAsleep / s.TimeInBedMinutes) : 0,
            scored.Count > 0 ? scored.Average(s => s.Score!.OverallScore) : null,
            sessions.Select(s => new SleepSessionListItemDto(
                s.Id.ToString(), s.StartUtc, s.EndUtc, s.SleepType, s.MinutesAsleep, s.MinutesAwake, s.TimeInBedMinutes,
                s.Score?.OverallScore, s.IsLegacy)).ToList()));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<SleepSessionDetailDto>> Detail(long id, CancellationToken ct)
    {
        await using var db = session.CreateContext();
        var s = await db.SleepSessions.Include(x => x.Score).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null)
        {
            return NotFound();
        }

        var stages = await db.SleepStages.Where(x => x.SleepSessionId == id).OrderBy(x => x.StartUtc).ToListAsync(ct);

        return Ok(new SleepSessionDetailDto(
            s.Id.ToString(), s.StartUtc, s.EndUtc, s.SleepType, s.DataSource, s.MinutesAsleep, s.MinutesAwake,
            s.MinutesToFallAsleep, s.MinutesAfterWakeup, s.TimeInBedMinutes, s.EfficiencyPercent, s.IsLegacy,
            stages.Select(x => new SleepStageDto(x.StageType, x.StartUtc, x.EndUtc)).ToList(),
            s.Score is null ? null : new SleepScoreDto(
                s.Score.OverallScore, s.Score.DurationScore, s.Score.CompositionScore, s.Score.RevitalizationScore,
                s.Score.DeepSleepMinutes, s.Score.RemSleepPercent, s.Score.RestingHeartRate, s.Score.RestlessnessNormalized)));
    }
}
