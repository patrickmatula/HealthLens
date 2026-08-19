using GoogleHealthLens.Api.Data;
using GoogleHealthLens.Api.Dtos;
using GoogleHealthLens.Api.Models;
using GoogleHealthLens.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoogleHealthLens.Api.Controllers;

[ApiController]
[Route("api/workouts")]
public class WorkoutsController(DataSessionService session) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkoutListItemDto>>> List(string? preset, DateOnly? from, DateOnly? to, string? activity, CancellationToken ct)
    {
        await using var db = session.CreateContext();

        if (!await db.Workouts.AnyAsync(ct))
        {
            return Ok(Array.Empty<WorkoutListItemDto>());
        }

        var earliest = DateOnly.FromDateTime(await db.Workouts.MinAsync(w => w.StartUtc, ct));
        var latest = DateOnly.FromDateTime(await db.Workouts.MaxAsync(w => w.StartUtc, ct));
        var range = TimeRange.Resolve(preset, from, to, earliest, latest);

        var query = db.Workouts.Where(w => w.StartUtc >= range.StartUtc && w.StartUtc <= range.EndUtc);
        if (!string.IsNullOrWhiteSpace(activity))
        {
            query = query.Where(w => w.ActivityName == activity);
        }

        var workouts = await query.OrderByDescending(w => w.StartUtc).ToListAsync(ct);

        return Ok(workouts.Select(w => new WorkoutListItemDto(
            w.Id.ToString(), w.StartUtc, w.EndUtc, w.DurationSeconds, w.ActivityName, w.DistanceMeters, w.Calories,
            w.AvgHeartRate, w.AvgPaceSecPerKm, w.HasGps, w.IsLegacy)).ToList());
    }

    [HttpGet("personal-records")]
    public async Task<ActionResult<IReadOnlyList<PersonalRecordDto>>> PersonalRecords(CancellationToken ct)
    {
        await using var db = session.CreateContext();
        var records = await db.PersonalRecords.OrderByDescending(r => r.AchieveTimeUtc).ToListAsync(ct);
        var workoutIds = records.Where(r => r.WorkoutId.HasValue).Select(r => r.WorkoutId!.Value).Distinct().ToList();
        var names = await db.Workouts.Where(w => workoutIds.Contains(w.Id)).ToDictionaryAsync(w => w.Id, w => w.ActivityName, ct);

        return Ok(records.Select(r => new PersonalRecordDto(
            r.Id, r.WorkoutId?.ToString(), r.NameLocalizationId, r.State, r.AchieveTimeUtc, r.RecordValue, r.RecordType, r.ExtentValueMeters,
            r.WorkoutId.HasValue && names.TryGetValue(r.WorkoutId.Value, out var n) ? n : null)).ToList());
    }

    // Distance-based records (fastest 1K/mile/2K/5K/10K/2mi): the true personal best across ALL of
    // Fitbit's own record states (STANDING/CANDIDATE/HISTORICAL both matter — a faster CANDIDATE that
    // hasn't been "promoted" yet is still the user's actual best time), reconciled against a simple
    // fallback computed directly from the workout list itself (a whole workout whose total distance is
    // within 2% of the target, using its own duration) — this catches runs Fitbit's own PR detection
    // hadn't (yet) recognized when this Takeout export was generated.
    [HttpGet("personal-records/best")]
    public async Task<ActionResult<IReadOnlyList<PersonalRecordDto>>> BestPersonalRecords(CancellationToken ct)
    {
        await using var db = session.CreateContext();
        var allRecords = await db.PersonalRecords.ToListAsync(ct);
        var allWorkouts = await db.Workouts.Where(w => w.DistanceMeters != null && w.DistanceMeters > 0).ToListAsync(ct);
        return Ok(ComputeBestRecords(allRecords, allWorkouts));
    }

    private static readonly (string Name, double Meters)[] TimeRecordTargets =
    [
        ("FASTEST_KILOMETRE_NAME", 1000),
        ("FASTEST_MILE_NAME", 1609.344),
        ("FASTEST_2_KILOMETRES_NAME", 2000),
        ("FASTEST_5K_NAME", 5000),
        ("FASTEST_10K_NAME", 10000),
        ("FASTEST_2_MILES_NAME", 3218.688),
    ];

    // Distance-based records (fastest 1K/mile/2K/5K/10K/2mi): the true personal best across ALL of
    // Fitbit's own record states (STANDING/CANDIDATE/HISTORICAL both matter — a faster CANDIDATE that
    // hasn't been "promoted" yet is still the user's actual best time), reconciled against a simple
    // fallback computed directly from the workout list itself (a whole workout whose total distance is
    // within 2% of the target, using its own duration) — this catches runs Fitbit's own PR detection
    // hadn't (yet) recognized when this Takeout export was generated.
    private static List<PersonalRecordDto> ComputeBestRecords(List<PersonalRecord> allRecords, List<Workout> allWorkouts)
    {
        var results = new List<PersonalRecordDto>();

        foreach (var (name, targetMeters) in TimeRecordTargets)
        {
            PersonalRecord? bestFitbit = allRecords
                .Where(r => r.NameLocalizationId == name && r.RecordType == "PERSONAL_RECORD_TYPE_SHORTEST_TIME_FOR_DISTANCE")
                .OrderBy(r => r.RecordValue)
                .FirstOrDefault();

            var bestWorkout = allWorkouts
                .Where(w => w.DistanceMeters >= targetMeters * 0.98 && w.DistanceMeters <= targetMeters * 1.02)
                .OrderBy(w => w.DurationSeconds)
                .FirstOrDefault();

            var fitbitMs = bestFitbit?.RecordValue;
            var workoutMs = bestWorkout?.DurationSeconds * 1000;

            if (fitbitMs is null && workoutMs is null)
            {
                continue;
            }

            if (workoutMs is not null && (fitbitMs is null || workoutMs < fitbitMs))
            {
                results.Add(new PersonalRecordDto(0, bestWorkout!.Id.ToString(), name, "PERSONAL_RECORD_STATE_STANDING", bestWorkout.EndUtc, workoutMs.Value, "PERSONAL_RECORD_TYPE_SHORTEST_TIME_FOR_DISTANCE", targetMeters, bestWorkout.ActivityName));
            }
            else if (bestFitbit is not null)
            {
                var workoutName = bestFitbit.WorkoutId.HasValue ? allWorkouts.FirstOrDefault(w => w.Id == bestFitbit.WorkoutId)?.ActivityName : null;
                results.Add(new PersonalRecordDto(bestFitbit.Id, bestFitbit.WorkoutId?.ToString(), name, "PERSONAL_RECORD_STATE_STANDING", bestFitbit.AchieveTimeUtc, bestFitbit.RecordValue, bestFitbit.RecordType, bestFitbit.ExtentValueMeters, workoutName));
            }
        }

        var bestFitbitDistance = allRecords
            .Where(r => r.NameLocalizationId == "FARTHEST_RUN_NAME" && r.RecordType == "PERSONAL_RECORD_TYPE_LONGEST_DISTANCE")
            .OrderByDescending(r => r.RecordValue)
            .FirstOrDefault();
        var farthestWorkout = allWorkouts.OrderByDescending(w => w.DistanceMeters).FirstOrDefault();

        var fitbitDistanceM = bestFitbitDistance?.RecordValue is { } mm ? mm / 1000.0 : (double?)null;
        var workoutDistanceM = farthestWorkout?.DistanceMeters;

        if (workoutDistanceM is not null && (fitbitDistanceM is null || workoutDistanceM > fitbitDistanceM))
        {
            results.Add(new PersonalRecordDto(0, farthestWorkout!.Id.ToString(), "FARTHEST_RUN_NAME", "PERSONAL_RECORD_STATE_STANDING", farthestWorkout.EndUtc, workoutDistanceM.Value * 1000, "PERSONAL_RECORD_TYPE_LONGEST_DISTANCE", null, farthestWorkout.ActivityName));
        }
        else if (bestFitbitDistance is not null)
        {
            var workoutName = bestFitbitDistance.WorkoutId.HasValue ? allWorkouts.FirstOrDefault(w => w.Id == bestFitbitDistance.WorkoutId)?.ActivityName : null;
            results.Add(new PersonalRecordDto(bestFitbitDistance.Id, bestFitbitDistance.WorkoutId?.ToString(), "FARTHEST_RUN_NAME", "PERSONAL_RECORD_STATE_STANDING", bestFitbitDistance.AchieveTimeUtc, bestFitbitDistance.RecordValue, bestFitbitDistance.RecordType, bestFitbitDistance.ExtentValueMeters, workoutName));
        }

        return results;
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<WorkoutDetailDto>> Detail(long id, CancellationToken ct)
    {
        await using var db = session.CreateContext();
        var workout = await db.Workouts.FindAsync([id], ct);
        if (workout is null)
        {
            return NotFound();
        }

        var splits = await db.WorkoutSplits.Where(s => s.WorkoutId == id).OrderBy(s => s.SplitIndex).ToListAsync(ct);
        // WorkoutSamples were merged in at import time with a +/-5min buffer around the workout's
        // recorded start/end (to catch GPS lock delay etc.) — trim back to the workout's own window
        // here, otherwise buffered pre/post-workout noise skews km splits and the detail charts.
        var samples = await db.WorkoutSamples
            .Where(s => s.WorkoutId == id && s.Timestamp >= workout.StartUtc && s.Timestamp <= workout.EndUtc)
            .OrderBy(s => s.Timestamp)
            .ToListAsync(ct);
        var records = await db.PersonalRecords.Where(r => r.WorkoutId == id).ToListAsync(ct);

        // Merge in any reconciled "best" records this exact workout holds (see ComputeBestRecords) that
        // aren't already present in its raw Fitbit-linked records — otherwise a workout whose PR Fitbit's
        // own export hadn't (yet) recognized would show its stat gauges but omit the record itself.
        var allRecords = await db.PersonalRecords.ToListAsync(ct);
        var allWorkouts = await db.Workouts.Where(w => w.DistanceMeters != null && w.DistanceMeters > 0).ToListAsync(ct);
        var existingNames = records.Select(r => r.NameLocalizationId).ToHashSet();
        var reconciledForThisWorkout = ComputeBestRecords(allRecords, allWorkouts)
            .Where(r => r.WorkoutId == workout.Id.ToString() && !existingNames.Contains(r.NameLocalizationId))
            .ToList();

        return Ok(new WorkoutDetailDto(
            workout.Id.ToString(), workout.StartUtc, workout.EndUtc, workout.DurationSeconds, workout.ActivityName, workout.LogType, workout.Source,
            workout.DistanceMeters, workout.Calories, workout.Steps, workout.AvgHeartRate, workout.PeakHeartRate,
            workout.AvgPaceSecPerKm, workout.AvgSpeedKmh, workout.PeakSpeedKmh, workout.ElevationGainMeters, workout.CardioLoad,
            workout.CadenceAvgSpm, workout.GroundContactTimeMs, workout.VerticalOscillationMm, workout.VerticalRatioPercent,
            workout.RatePerceivedExertion, workout.HasGps, workout.IsLegacy,
            splits.Select(s => new WorkoutSplitDto(s.SplitIndex, s.Type, s.Timestamp, s.ElapsedMs, s.DistanceMeters, s.Calories, s.Steps, s.AvgHeartRate, s.ElevationGainMeters, s.AvgSpeedKmh)).ToList(),
            ComputeKmSplits(samples),
            samples.Select(s => new WorkoutSampleDto(s.Timestamp, s.Latitude, s.Longitude, s.AltitudeMeters, s.HeartRateBpm, s.PaceSecPerKm, s.CadenceSpm, s.SpeedKmh, s.StrideLengthCm, s.VerticalOscillationCm, s.VerticalRatioPercent, s.GroundContactTimeMs)).ToList(),
            records.Select(r => new PersonalRecordDto(r.Id, r.WorkoutId?.ToString(), r.NameLocalizationId, r.State, r.AchieveTimeUtc, r.RecordValue, r.RecordType, r.ExtentValueMeters, workout.ActivityName))
                .Concat(reconciledForThisWorkout)
                .ToList()));
    }

    // WorkoutSplits (from UserExercises' embedded events) is often empty, so this derives km-by-km
    // splits directly from the per-second samples: cumulative distance via haversine between
    // consecutive GPS fixes (falling back to integrating instantaneous speed when GPS is missing,
    // e.g. treadmill runs), then linearly interpolates the exact moment each 1km boundary is crossed.
    private static List<KmSplitDto> ComputeKmSplits(List<WorkoutSample> samples)
    {
        var ordered = samples.OrderBy(s => s.Timestamp).ToList();
        if (ordered.Count < 2)
        {
            return [];
        }

        var points = new List<(DateTime Ts, double CumMeters, double? Hr)>(ordered.Count);
        double cumulative = 0;
        DateTime? prevTs = null;
        double? prevLat = null;
        double? prevLon = null;

        foreach (var s in ordered)
        {
            if (prevTs is not null)
            {
                if (s.Latitude is not null && s.Longitude is not null && prevLat is not null && prevLon is not null)
                {
                    cumulative += HaversineMeters(prevLat.Value, prevLon.Value, s.Latitude.Value, s.Longitude.Value);
                }
                else if (s.SpeedKmh is not null)
                {
                    var dtSeconds = (s.Timestamp - prevTs.Value).TotalSeconds;
                    cumulative += s.SpeedKmh.Value * (1000.0 / 3600.0) * dtSeconds;
                }
            }

            points.Add((s.Timestamp, cumulative, s.HeartRateBpm));
            prevTs = s.Timestamp;
            if (s.Latitude is not null && s.Longitude is not null)
            {
                prevLat = s.Latitude;
                prevLon = s.Longitude;
            }
        }

        var splits = new List<KmSplitDto>();
        var nextKmMeters = 1000.0;
        var segmentStart = points[0].Ts;
        var hrSum = 0.0;
        var hrCount = 0;

        for (var i = 1; i < points.Count; i++)
        {
            if (points[i].Hr is { } hr)
            {
                hrSum += hr;
                hrCount++;
            }

            if (points[i].CumMeters < nextKmMeters)
            {
                continue;
            }

            var prev = points[i - 1];
            var cur = points[i];
            var frac = cur.CumMeters > prev.CumMeters ? (nextKmMeters - prev.CumMeters) / (cur.CumMeters - prev.CumMeters) : 0;
            var crossingTime = prev.Ts + TimeSpan.FromSeconds((cur.Ts - prev.Ts).TotalSeconds * frac);

            splits.Add(new KmSplitDto((int)(nextKmMeters / 1000), (crossingTime - segmentStart).TotalSeconds, hrCount > 0 ? hrSum / hrCount : null));

            segmentStart = crossingTime;
            hrSum = 0;
            hrCount = 0;
            nextKmMeters += 1000;
        }

        return splits;
    }

    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMeters = 6371000;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusMeters * c;
    }
}
