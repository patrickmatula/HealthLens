using GoogleHealthLens.Api.Data;
using GoogleHealthLens.Api.Dtos;
using GoogleHealthLens.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoogleHealthLens.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController(DataSessionService session) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<DashboardOverviewDto>> Overview(string? preset, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        await using var db = session.CreateContext();

        if (!await db.DailyActivitySummaries.AnyAsync(ct))
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            return Ok(new DashboardOverviewDto(today, today, 0, 0, 0, 0, 0, 0, [], 0, null, null, []));
        }

        var earliest = await db.DailyActivitySummaries.MinAsync(d => d.Date, ct);
        var latest = await db.DailyActivitySummaries.MaxAsync(d => d.Date, ct);
        var range = TimeRange.Resolve(preset, from, to, earliest, latest);

        var days = await db.DailyActivitySummaries
            .Where(d => d.Date >= range.From && d.Date <= range.To)
            .OrderBy(d => d.Date)
            .ToListAsync(ct);

        var withData = days.Count(d => d.Steps is > 0);

        var workoutsInRange = await db.Workouts.CountAsync(w => w.StartUtc >= range.StartUtc && w.StartUtc <= range.EndUtc, ct);

        var sleepScores = await db.SleepSessions
            .Where(s => s.EndUtc >= range.StartUtc && s.EndUtc <= range.EndUtc && s.Score != null)
            .Select(s => s.Score!.OverallScore)
            .ToListAsync(ct);
        double? avgSleepScore = sleepScores.Count > 0 ? sleepScores.Average() : null;

        var rhrInRange = await db.RestingHeartRateDailies.Where(x => x.Date >= range.From && x.Date <= range.To).ToListAsync(ct);
        double? avgRestingHr = rhrInRange.Count > 0 ? rhrInRange.Average(x => x.Bpm) : null;

        var insights = new List<string>();

        var newRecords = await db.PersonalRecords
            .Where(r => r.AchieveTimeUtc >= range.StartUtc && r.AchieveTimeUtc <= range.EndUtc && r.State == "PERSONAL_RECORD_STATE_STANDING")
            .CountAsync(ct);
        if (newRecords > 0)
        {
            insights.Add($"{newRecords} neue Bestleistung(en) in diesem Zeitraum erreicht.");
        }

        if (rhrInRange.Count >= 14)
        {
            var ordered = rhrInRange.OrderBy(x => x.Date).ToList();
            var half = ordered.Count / 2;
            var firstHalfAvg = ordered.Take(half).Average(x => x.Bpm);
            var secondHalfAvg = ordered.Skip(half).Average(x => x.Bpm);
            var delta = secondHalfAvg - firstHalfAvg;
            if (Math.Abs(delta) >= 1.5)
            {
                var direction = delta < 0 ? "gesunken" : "gestiegen";
                insights.Add($"Ruhepuls ist im Verlauf dieses Zeitraums um {Math.Abs(delta):F1} bpm {direction}.");
            }
        }

        return Ok(new DashboardOverviewDto(
            range.From,
            range.To,
            withData,
            days.Sum(d => (long)(d.Steps ?? 0)),
            days.Sum(d => d.DistanceMeters ?? 0),
            days.Sum(d => d.CaloriesTotal ?? 0),
            withData > 0 ? days.Sum(d => d.Steps ?? 0) / (double)withData : 0,
            withData > 0 ? days.Sum(d => d.ActiveMinutes ?? 0) / (double)withData : 0,
            days.Select(d => new DailyActivityPointDto(d.Date, d.Steps, d.DistanceMeters, d.CaloriesTotal, d.ActiveMinutes, d.SedentaryMinutes)).ToList(),
            workoutsInRange,
            avgSleepScore,
            avgRestingHr,
            insights));
    }
}
