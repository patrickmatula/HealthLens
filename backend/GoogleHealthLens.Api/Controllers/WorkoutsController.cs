using GoogleHealthLens.Api.Data;
using GoogleHealthLens.Api.Dtos;
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
        var rangeStart = range.From.ToDateTime(TimeOnly.MinValue);
        var rangeEnd = range.To.ToDateTime(TimeOnly.MaxValue);

        var query = db.Workouts.Where(w => w.StartUtc >= rangeStart && w.StartUtc <= rangeEnd);
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
        var samples = await db.WorkoutSamples.Where(s => s.WorkoutId == id).OrderBy(s => s.Timestamp).ToListAsync(ct);
        var records = await db.PersonalRecords.Where(r => r.WorkoutId == id).ToListAsync(ct);

        return Ok(new WorkoutDetailDto(
            workout.Id.ToString(), workout.StartUtc, workout.EndUtc, workout.DurationSeconds, workout.ActivityName, workout.LogType, workout.Source,
            workout.DistanceMeters, workout.Calories, workout.Steps, workout.AvgHeartRate, workout.PeakHeartRate,
            workout.AvgPaceSecPerKm, workout.AvgSpeedKmh, workout.PeakSpeedKmh, workout.ElevationGainMeters, workout.CardioLoad,
            workout.CadenceAvgSpm, workout.GroundContactTimeMs, workout.VerticalOscillationMm, workout.VerticalRatioPercent,
            workout.RatePerceivedExertion, workout.HasGps, workout.IsLegacy,
            splits.Select(s => new WorkoutSplitDto(s.SplitIndex, s.Type, s.Timestamp, s.ElapsedMs, s.DistanceMeters, s.Calories, s.Steps, s.AvgHeartRate, s.ElevationGainMeters, s.AvgSpeedKmh)).ToList(),
            samples.Select(s => new WorkoutSampleDto(s.Timestamp, s.Latitude, s.Longitude, s.AltitudeMeters, s.HeartRateBpm, s.PaceSecPerKm, s.CadenceSpm, s.SpeedKmh, s.StrideLengthCm, s.VerticalOscillationCm, s.VerticalRatioPercent, s.GroundContactTimeMs)).ToList(),
            records.Select(r => new PersonalRecordDto(r.Id, r.WorkoutId?.ToString(), r.NameLocalizationId, r.State, r.AchieveTimeUtc, r.RecordValue, r.RecordType, r.ExtentValueMeters, workout.ActivityName)).ToList()));
    }
}
