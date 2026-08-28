using HealthLens.Api.Data;
using HealthLens.Api.Dtos;
using HealthLens.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthLens.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController(DataSessionService session) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<DashboardOverviewDto>> Overview(string? preset, DateOnly? from, DateOnly? to, string? lang, CancellationToken ct)
    {
        var isEnglish = lang == "en";
        await using var db = session.CreateContext();

        if (!await db.DailyActivitySummaries.AnyAsync(ct))
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            return Ok(new DashboardOverviewDto(today, today, 0, 0, 0, 0, 0, 0, [], 0, null, null, [], []));
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
        var rhrByDate = rhrInRange.GroupBy(x => x.Date).ToDictionary(g => g.Key, g => (double?)g.Average(x => x.Bpm));

        var sleepScoresByDate = await db.SleepSessions
            .Where(s => s.EndUtc >= range.StartUtc && s.EndUtc <= range.EndUtc && s.Score != null)
            .Select(s => new { Date = DateOnly.FromDateTime(s.EndUtc), Score = s.Score!.OverallScore })
            .ToListAsync(ct);
        var sleepScoreByDate = sleepScoresByDate.GroupBy(x => x.Date).ToDictionary(g => g.Key, g => (double?)g.Average(x => x.Score));

        var workoutsForBreakdown = await db.Workouts
            .Where(w => w.StartUtc >= range.StartUtc && w.StartUtc <= range.EndUtc)
            .Select(w => new { w.ActivityName, w.AvgPaceSecPerKm, w.CadenceAvgSpm })
            .ToListAsync(ct);
        var activityBreakdown = workoutsForBreakdown
            .Select(w => WorkoutCategorizer.Categorize(w.ActivityName, w.AvgPaceSecPerKm, w.CadenceAvgSpm))
            .GroupBy(c => c)
            .Select(g => new ActivityCategoryCountDto(g.Key, g.Count()))
            .OrderByDescending(c => c.Count)
            .ToList();

        var insights = new List<string>();

        var newRecords = await db.PersonalRecords
            .Where(r => r.AchieveTimeUtc >= range.StartUtc && r.AchieveTimeUtc <= range.EndUtc && r.State == "PERSONAL_RECORD_STATE_STANDING")
            .CountAsync(ct);
        if (newRecords > 0)
        {
            insights.Add(isEnglish
                ? $"{newRecords} new personal best(s) reached in this time range."
                : $"{newRecords} neue Bestleistung(en) in diesem Zeitraum erreicht.");
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
                if (isEnglish)
                {
                    var direction = delta < 0 ? "decreased" : "increased";
                    insights.Add($"Resting heart rate has {direction} by {Math.Abs(delta):F1} bpm over this time range.");
                }
                else
                {
                    var direction = delta < 0 ? "gesunken" : "gestiegen";
                    insights.Add($"Ruhepuls ist im Verlauf dieses Zeitraums um {Math.Abs(delta):F1} bpm {direction}.");
                }
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
            days.Select(d => new DailyActivityPointDto(
                d.Date, d.Steps, d.DistanceMeters, d.CaloriesTotal, d.ActiveMinutes, d.SedentaryMinutes,
                rhrByDate.GetValueOrDefault(d.Date), sleepScoreByDate.GetValueOrDefault(d.Date))).ToList(),
            workoutsInRange,
            avgSleepScore,
            avgRestingHr,
            insights,
            activityBreakdown));
    }

    /// <summary>The starting GPS fix of every GPS-tracked workout in range, for the Dashboard's "where have
    /// you trained" map. Per-workout lookups rather than one big samples query: WorkoutSamples is a
    /// per-second table, so pulling every sample just to keep the first would move far more data than
    /// needed for what's realistically at most a few hundred GPS workouts on a personal install.</summary>
    [HttpGet("workout-locations")]
    public async Task<ActionResult<IReadOnlyList<WorkoutLocationDto>>> WorkoutLocations(string? preset, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        await using var db = session.CreateContext();

        if (!await db.Workouts.AnyAsync(w => w.HasGps, ct))
        {
            return Ok(Array.Empty<WorkoutLocationDto>());
        }

        var earliest = DateOnly.FromDateTime(await db.Workouts.MinAsync(w => w.StartUtc, ct));
        var latest = DateOnly.FromDateTime(await db.Workouts.MaxAsync(w => w.StartUtc, ct));
        var range = TimeRange.Resolve(preset, from, to, earliest, latest);

        var workoutIds = await db.Workouts
            .Where(w => w.HasGps && w.StartUtc >= range.StartUtc && w.StartUtc <= range.EndUtc)
            .Select(w => w.Id)
            .ToListAsync(ct);

        var locations = new List<WorkoutLocationDto>();
        foreach (var id in workoutIds)
        {
            var first = await db.WorkoutSamples
                .Where(s => s.WorkoutId == id && s.Latitude != null && s.Longitude != null)
                .OrderBy(s => s.Timestamp)
                .Select(s => new { s.Latitude, s.Longitude })
                .FirstOrDefaultAsync(ct);

            if (first is not null)
            {
                locations.Add(new WorkoutLocationDto(id.ToString(), first.Latitude!.Value, first.Longitude!.Value));
            }
        }

        return Ok(locations);
    }
}
