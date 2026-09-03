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

        // Illness/overtraining early warning: a resting-HR rise combined with an HRV drop over the last
        // few days is a well-documented early signal (often 1-2 days before symptoms), so this is checked
        // against a fixed trailing window independent of the selected preset -- it should surface
        // regardless of whether the user happens to be looking at "7 days" or "all time" right now.
        if (await db.RestingHeartRateDailies.AnyAsync(ct))
        {
            var refDate = await db.RestingHeartRateDailies.MaxAsync(x => x.Date, ct);
            var checkWindow = new TimeRange(refDate.AddDays(-16), refDate);
            var rhrCheck = await db.RestingHeartRateDailies.Where(x => x.Date >= checkWindow.From && x.Date <= checkWindow.To).ToListAsync(ct);
            var hrvCheck = await db.HrvDailySummaries.Where(x => x.Date >= checkWindow.From && x.Date <= checkWindow.To).ToListAsync(ct);

            var recentCutoff = refDate.AddDays(-3);
            var recentRhr = rhrCheck.Where(x => x.Date > recentCutoff).ToList();
            var baselineRhr = rhrCheck.Where(x => x.Date <= recentCutoff).ToList();
            var recentHrv = hrvCheck.Where(x => x.Date > recentCutoff).ToList();
            var baselineHrv = hrvCheck.Where(x => x.Date <= recentCutoff).ToList();

            if (recentRhr.Count >= 2 && baselineRhr.Count >= 5 && recentHrv.Count >= 2 && baselineHrv.Count >= 5)
            {
                var rhrDelta = recentRhr.Average(x => x.Bpm) - baselineRhr.Average(x => x.Bpm);
                var baselineHrvAvg = baselineHrv.Average(x => x.RmssdMs);
                var hrvDeltaPercent = baselineHrvAvg > 0 ? (recentHrv.Average(x => x.RmssdMs) - baselineHrvAvg) / baselineHrvAvg * 100 : 0;

                if (rhrDelta >= 2 && hrvDeltaPercent <= -15)
                {
                    insights.Add(isEnglish
                        ? $"Resting HR is up {rhrDelta:F1} bpm and HRV is down {Math.Abs(hrvDeltaPercent):F0}% over the last few days — a common early sign of illness or overtraining. Consider an easy day."
                        : $"Ruhepuls ist in den letzten Tagen um {rhrDelta:F1} bpm gestiegen, die HRV um {Math.Abs(hrvDeltaPercent):F0}% gefallen — ein typisches Frühzeichen für Krankheit oder Übertraining. Ein ruhiger Tag könnte guttun.");
                }
            }
        }

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

    /// <summary>Daily activity for a GitHub-contributions-style consistency calendar — always the trailing
    /// N days ending on the most recent day with data, independent of the Dashboard's own timeframe
    /// preset (a consistency view only makes sense over a fixed, comparable window).</summary>
    [HttpGet("consistency-heatmap")]
    public async Task<ActionResult<IReadOnlyList<ConsistencyDayDto>>> ConsistencyHeatmap(int? days, CancellationToken ct)
    {
        await using var db = session.CreateContext();

        if (!await db.DailyActivitySummaries.AnyAsync(ct))
        {
            return Ok(Array.Empty<ConsistencyDayDto>());
        }

        var latest = await db.DailyActivitySummaries.MaxAsync(d => d.Date, ct);
        var windowDays = days is > 0 ? days.Value : 365;
        var range = new TimeRange(latest.AddDays(-(windowDays - 1)), latest);

        var stepsByDate = await db.DailyActivitySummaries
            .Where(d => d.Date >= range.From && d.Date <= range.To)
            .ToDictionaryAsync(d => d.Date, d => d.Steps, ct);

        var workoutStarts = await db.Workouts
            .Where(w => w.StartUtc >= range.StartUtc && w.StartUtc <= range.EndUtc)
            .Select(w => w.StartUtc)
            .ToListAsync(ct);
        var workoutCountByDate = workoutStarts
            .GroupBy(t => DateOnly.FromDateTime(t))
            .ToDictionary(g => g.Key, g => g.Count());

        var result = new List<ConsistencyDayDto>();
        for (var d = range.From; d <= range.To; d = d.AddDays(1))
        {
            result.Add(new ConsistencyDayDto(d, stepsByDate.GetValueOrDefault(d), workoutCountByDate.GetValueOrDefault(d)));
        }

        return Ok(result);
    }

    /// <summary>All-time totals for the Dashboard's "fun distance comparisons" gimmick (marathons run,
    /// % of the way to the moon, etc.) -- deliberately lifetime, not scoped to the timeframe preset, since
    /// "you've run to the moon" only means something as a running lifetime total.</summary>
    [HttpGet("fun-facts")]
    public async Task<ActionResult<FunFactsDto>> FunFacts(CancellationToken ct)
    {
        await using var db = session.CreateContext();

        if (!await db.Workouts.AnyAsync(ct))
        {
            return Ok(new FunFactsDto(0, 0, 0));
        }

        var totalDistance = await db.Workouts.SumAsync(w => w.DistanceMeters ?? 0, ct);
        var totalElevation = await db.Workouts.SumAsync(w => w.ElevationGainMeters ?? 0, ct);
        var totalWorkouts = await db.Workouts.CountAsync(ct);

        return Ok(new FunFactsDto(totalDistance, totalElevation, totalWorkouts));
    }

    /// <summary>"On this day in a past year" -- the most recent past workout that happened on today's own
    /// calendar day (month+day, any earlier year). A nice-to-have nostalgia hit, not core data, so it
    /// simply omits itself (HasFlashback: false) rather than erroring when there's no match.</summary>
    [HttpGet("flashback")]
    public async Task<ActionResult<FlashbackDto>> Flashback(CancellationToken ct)
    {
        await using var db = session.CreateContext();
        var today = DateTime.UtcNow;

        var candidates = await db.Workouts
            .Where(w => w.StartUtc.Month == today.Month && w.StartUtc.Day == today.Day && w.StartUtc.Year < today.Year)
            .OrderByDescending(w => w.StartUtc)
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            return Ok(new FlashbackDto(false, null, null, null, null, null));
        }

        var best = candidates[0];
        return Ok(new FlashbackDto(true, best.StartUtc, best.ActivityName, best.DistanceMeters, best.DurationSeconds, today.Year - best.StartUtc.Year));
    }

    // 8h/night is the standard adult sleep recommendation (matches the threshold most sleep-debt
    // trackers default to); HealthLens has no per-user settings store yet to make this configurable.
    private const double TargetSleepMinutesPerNight = 8 * 60;

    /// <summary>A fixed trailing-7-days digest, independent of the Dashboard's own timeframe preset --
    /// "how was my week" should always mean the same thing regardless of what range is selected above it.</summary>
    [HttpGet("weekly-digest")]
    public async Task<ActionResult<WeeklyDigestDto>> WeeklyDigest(CancellationToken ct)
    {
        await using var db = session.CreateContext();

        if (!await db.DailyActivitySummaries.AnyAsync(ct))
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            return Ok(new WeeklyDigestDto(today, today, 0, 0, 0, 0, 0, null, null, null));
        }

        var latest = await db.DailyActivitySummaries.MaxAsync(d => d.Date, ct);
        var thisWeek = new TimeRange(latest.AddDays(-6), latest);
        var priorWeek = new TimeRange(latest.AddDays(-13), latest.AddDays(-7));

        var days = await db.DailyActivitySummaries.Where(d => d.Date >= thisWeek.From && d.Date <= thisWeek.To).ToListAsync(ct);
        var totalDistance = days.Sum(d => d.DistanceMeters ?? 0);
        var activeDays = days.Count(d => (d.Steps ?? 0) > 0);

        var workoutsCount = await db.Workouts.CountAsync(w => w.StartUtc >= thisWeek.StartUtc && w.StartUtc <= thisWeek.EndUtc, ct);

        var sleepSessions = await db.SleepSessions
            .Where(s => s.EndUtc >= thisWeek.StartUtc && s.EndUtc <= thisWeek.EndUtc)
            .ToListAsync(ct);
        var totalSleepMinutes = sleepSessions.Sum(s => s.MinutesAsleep);
        var sleepDebt = Math.Max(0, TargetSleepMinutesPerNight * 7 - totalSleepMinutes);

        var weekScores = sleepSessions.Where(s => s.Score != null).Select(s => s.Score!.OverallScore).ToList();
        double? avgSleepScore = weekScores.Count > 0 ? weekScores.Average() : null;

        var rhrThisWeek = await db.RestingHeartRateDailies.Where(x => x.Date >= thisWeek.From && x.Date <= thisWeek.To).ToListAsync(ct);
        double? avgRhr = rhrThisWeek.Count > 0 ? rhrThisWeek.Average(x => x.Bpm) : null;

        var rhrPriorWeek = await db.RestingHeartRateDailies.Where(x => x.Date >= priorWeek.From && x.Date <= priorWeek.To).ToListAsync(ct);
        double? avgRhrPrior = rhrPriorWeek.Count > 0 ? rhrPriorWeek.Average(x => x.Bpm) : null;
        double? rhrDelta = avgRhr != null && avgRhrPrior != null ? avgRhr - avgRhrPrior : null;

        return Ok(new WeeklyDigestDto(thisWeek.From, thisWeek.To, totalDistance, workoutsCount, activeDays, totalSleepMinutes, sleepDebt, avgSleepScore, avgRhr, rhrDelta));
    }

    /// <summary>Acute:chronic workload ratio -- last 7 days' training minutes vs. the average weekly total
    /// over the last 28 days. 0.8-1.3 is the commonly cited "sweet spot" (fitness building without a load
    /// spike); >=1.5 is associated with meaningfully higher injury risk in the sports-science literature.
    /// Minutes rather than distance as the load unit, since it's the one measure that's meaningful across
    /// every activity type this app tracks (running distance doesn't mean anything for a strength session).</summary>
    [HttpGet("training-load")]
    public async Task<ActionResult<TrainingLoadDto>> TrainingLoad(CancellationToken ct)
    {
        await using var db = session.CreateContext();

        if (!await db.Workouts.AnyAsync(ct))
        {
            return Ok(new TrainingLoadDto(0, 0, null, "insufficient-data"));
        }

        var latest = DateOnly.FromDateTime(await db.Workouts.MaxAsync(w => w.StartUtc, ct));
        var acuteRange = new TimeRange(latest.AddDays(-6), latest);
        var chronicRange = new TimeRange(latest.AddDays(-27), latest);

        var acuteWorkouts = await db.Workouts
            .Where(w => w.StartUtc >= acuteRange.StartUtc && w.StartUtc <= acuteRange.EndUtc)
            .Select(w => new { w.StartUtc, w.EndUtc })
            .ToListAsync(ct);
        var acuteMinutes = acuteWorkouts.Sum(w => (w.EndUtc - w.StartUtc).TotalMinutes);

        var chronicWorkouts = await db.Workouts
            .Where(w => w.StartUtc >= chronicRange.StartUtc && w.StartUtc <= chronicRange.EndUtc)
            .Select(w => new { w.StartUtc, w.EndUtc })
            .ToListAsync(ct);
        var chronicWeeklyAvg = chronicWorkouts.Sum(w => (w.EndUtc - w.StartUtc).TotalMinutes) / 4.0;

        double? acwr = chronicWeeklyAvg > 0 ? acuteMinutes / chronicWeeklyAvg : null;
        var zone = acwr switch
        {
            null => "insufficient-data",
            < 0.8 => "low",
            <= 1.3 => "sweet-spot",
            < 1.5 => "elevated",
            _ => "high-risk",
        };

        return Ok(new TrainingLoadDto(acuteMinutes, chronicWeeklyAvg, acwr, zone));
    }

    /// <summary>A Strava-"Year in Sport"-style annual recap, free (Strava's own equivalent moved behind a
    /// paywall) since every input already lives in this app's own database. `year` defaults to the most
    /// recent year with any workout data; EarliestYear/LatestYear let the frontend build a year picker
    /// without a separate round-trip.</summary>
    [HttpGet("year-in-review")]
    public async Task<ActionResult<YearInReviewDto>> YearInReview(int? year, CancellationToken ct)
    {
        await using var db = session.CreateContext();

        if (!await db.Workouts.AnyAsync(ct))
        {
            var thisYear = DateTime.UtcNow.Year;
            return Ok(new YearInReviewDto(thisYear, thisYear, thisYear, false, 0, 0, 0, 0, null, null, 0, null, []));
        }

        var allStarts = await db.Workouts.Select(w => w.StartUtc).ToListAsync(ct);
        var earliestYear = allStarts.Min(d => d.Year);
        var latestYear = allStarts.Max(d => d.Year);
        var targetYear = year ?? latestYear;

        var yearStart = DateTime.SpecifyKind(new DateTime(targetYear, 1, 1), DateTimeKind.Utc);
        var yearEnd = DateTime.SpecifyKind(new DateTime(targetYear, 12, 31, 23, 59, 59), DateTimeKind.Utc);

        var workouts = await db.Workouts
            .Where(w => w.StartUtc >= yearStart && w.StartUtc <= yearEnd)
            .Select(w => new { w.StartUtc, w.DistanceMeters, w.ElevationGainMeters, w.ActivityName, w.AvgPaceSecPerKm, w.CadenceAvgSpm })
            .ToListAsync(ct);

        if (workouts.Count == 0)
        {
            return Ok(new YearInReviewDto(targetYear, earliestYear, latestYear, false, 0, 0, 0, 0, null, null, 0, null, []));
        }

        var totalDistance = workouts.Sum(w => w.DistanceMeters ?? 0);
        var totalElevation = workouts.Sum(w => w.ElevationGainMeters ?? 0);
        var activeDays = workouts.Select(w => w.StartUtc.Date).Distinct().Count();
        var longestRun = workouts.Where(w => w.DistanceMeters != null).Select(w => w.DistanceMeters).DefaultIfEmpty().Max();

        var bestMonthGroup = workouts.GroupBy(w => w.StartUtc.Month).OrderByDescending(g => g.Count()).First();

        var priorYearStart = yearStart.AddYears(-1);
        var priorYearEnd = yearEnd.AddYears(-1);
        var priorYearDistance = await db.Workouts
            .Where(w => w.StartUtc >= priorYearStart && w.StartUtc <= priorYearEnd)
            .Select(w => w.DistanceMeters ?? 0)
            .ToListAsync(ct);

        var activityBreakdown = workouts
            .Select(w => WorkoutCategorizer.Categorize(w.ActivityName, w.AvgPaceSecPerKm, w.CadenceAvgSpm))
            .GroupBy(c => c)
            .Select(g => new ActivityCategoryCountDto(g.Key, g.Count()))
            .OrderByDescending(c => c.Count)
            .ToList();

        return Ok(new YearInReviewDto(
            targetYear, earliestYear, latestYear, true,
            totalDistance, workouts.Count, activeDays, totalElevation, longestRun,
            bestMonthGroup.Key, bestMonthGroup.Count(),
            priorYearDistance.Count > 0 ? priorYearDistance.Sum() : null,
            activityBreakdown));
    }
}
