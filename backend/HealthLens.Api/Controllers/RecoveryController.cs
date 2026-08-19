using HealthLens.Api.Data;
using HealthLens.Api.Dtos;
using HealthLens.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthLens.Api.Controllers;

[ApiController]
[Route("api/recovery")]
public class RecoveryController(DataSessionService session) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<RecoveryOverviewDto>> Overview(string? preset, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        await using var db = session.CreateContext();

        var dates = new List<DateOnly>();

        if (!await db.StressScoreDailies.AnyAsync(ct) && !await db.DailyReadinesses.AnyAsync(ct) &&
            !await db.SpO2Dailies.AnyAsync(ct) && !await db.TemperatureNightlies.AnyAsync(ct))
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            return Ok(new RecoveryOverviewDto(today, today, [], [], [], []));
        }

        if (await db.StressScoreDailies.AnyAsync(ct)) { dates.Add(await db.StressScoreDailies.MinAsync(x => x.Date, ct)); dates.Add(await db.StressScoreDailies.MaxAsync(x => x.Date, ct)); }
        if (await db.DailyReadinesses.AnyAsync(ct)) { dates.Add(await db.DailyReadinesses.MinAsync(x => x.Date, ct)); dates.Add(await db.DailyReadinesses.MaxAsync(x => x.Date, ct)); }
        if (await db.SpO2Dailies.AnyAsync(ct)) { dates.Add(await db.SpO2Dailies.MinAsync(x => x.Date, ct)); dates.Add(await db.SpO2Dailies.MaxAsync(x => x.Date, ct)); }
        if (await db.TemperatureNightlies.AnyAsync(ct)) { dates.Add(await db.TemperatureNightlies.MinAsync(x => x.Date, ct)); dates.Add(await db.TemperatureNightlies.MaxAsync(x => x.Date, ct)); }

        var range = TimeRange.Resolve(preset, from, to, dates.Min(), dates.Max());

        var stress = await db.StressScoreDailies.Where(x => x.Date >= range.From && x.Date <= range.To).OrderBy(x => x.Date).ToListAsync(ct);
        var readiness = await db.DailyReadinesses.Where(x => x.Date >= range.From && x.Date <= range.To).OrderBy(x => x.Date).ToListAsync(ct);
        var spo2 = await db.SpO2Dailies.Where(x => x.Date >= range.From && x.Date <= range.To).OrderBy(x => x.Date).ToListAsync(ct);
        var temp = await db.TemperatureNightlies.Where(x => x.Date >= range.From && x.Date <= range.To).OrderBy(x => x.Date).ToListAsync(ct);

        return Ok(new RecoveryOverviewDto(
            range.From,
            range.To,
            stress.Select(x => new StressScorePointDto(x.Date, x.Score)).ToList(),
            readiness.Select(x => new ReadinessPointDto(x.Date, x.Score, x.Level)).ToList(),
            spo2.Select(x => new SpO2PointDto(x.Date, x.AveragePercent, x.LowerBoundPercent, x.UpperBoundPercent)).ToList(),
            temp.Select(x => new TemperaturePointDto(x.Date, x.NightlyCelsius, x.BaselineCelsius, x.BaselineCelsius.HasValue ? x.NightlyCelsius - x.BaselineCelsius.Value : null)).ToList()));
    }

    [HttpGet("correlation")]
    public async Task<ActionResult<IReadOnlyList<CorrelationPointDto>>> Correlation(CancellationToken ct)
    {
        await using var db = session.CreateContext();

        var sleepScores = await db.SleepSessions
            .Where(s => s.Score != null)
            .Select(s => new { Date = DateOnly.FromDateTime(s.EndUtc), Score = s.Score!.OverallScore })
            .ToListAsync(ct);

        if (sleepScores.Count == 0)
        {
            return Ok(Array.Empty<CorrelationPointDto>());
        }

        var rhrByDate = await db.RestingHeartRateDailies.ToDictionaryAsync(x => x.Date, x => x.Bpm, ct);
        var stressByDate = await db.StressScoreDailies.ToDictionaryAsync(x => x.Date, x => x.Score, ct);
        var readinessByDate = await db.DailyReadinesses.ToDictionaryAsync(x => x.Date, x => x.Score, ct);

        var points = sleepScores
            .GroupBy(s => s.Date)
            .Select(g => new CorrelationPointDto(
                g.Key,
                g.Average(x => x.Score),
                rhrByDate.GetValueOrDefault(g.Key),
                stressByDate.TryGetValue(g.Key, out var st) ? st : null,
                readinessByDate.TryGetValue(g.Key, out var rd) ? rd : null))
            .OrderBy(p => p.Date)
            .ToList();

        return Ok(points);
    }
}
