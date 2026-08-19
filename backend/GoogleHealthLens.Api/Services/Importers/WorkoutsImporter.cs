using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CsvHelper;
using GoogleHealthLens.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GoogleHealthLens.Api.Services.Importers;

/// <summary>
/// Builds the Workouts / WorkoutSplits / WorkoutSamples / PersonalRecords tables from three
/// modern sources (UserExercises, WorkoutSummariesAndRounds, PersonalRecords) plus a time-range
/// join against the per-second running-dynamics/GPS/heart-rate streams, then gap-fills pre-2025
/// history from the legacy Fitbit exercise-*.json export.
/// </summary>
public partial class WorkoutsImporter : IDomainImporter
{
    public string Name => "Workouts";

    public async Task ImportAsync(ImportContext context, CancellationToken ct)
    {
        var hfd = context.FindFolder("Health Fitness Data_GoogleData");
        var pa = context.FindFolder("Physical Activity_GoogleData");
        var legacy = context.FindFolder("Global Export Data");

        var workouts = new Dictionary<long, Workout>();
        var splits = new Dictionary<long, List<WorkoutSplit>>();
        var records = new List<PersonalRecord>();
        var samples = new Dictionary<long, Dictionary<DateTime, WorkoutSample>>();

        if (hfd is not null)
        {
            context.ReportProgress("Workouts (UserExercises) werden gelesen...", 0);
            ParseUserExercises(hfd, workouts, splits);

            context.ReportProgress("Lauf-Dynamik (WorkoutSummariesAndRounds) wird verknüpft...", 15);
            ParseWorkoutSummariesAndRounds(hfd, workouts);

            context.ReportProgress("Bestleistungen werden gelesen...", 25);
            ParsePersonalRecords(hfd, workouts, records);
        }

        if (pa is not null)
        {
            context.ReportProgress("GPS & Lauf-Sensordaten werden den Workouts zugeordnet...", 35);
            ImportPerSecondStreams(pa, workouts, samples);
        }

        if (legacy is not null)
        {
            context.ReportProgress("Ältere Workouts (Legacy-Export) werden ergänzt...", 70);
            ImportLegacyExercises(legacy, workouts);
        }

        context.ReportProgress("Workouts werden gespeichert...", 85);
        await using var db = context.CreateContext();

        foreach (var workout in workouts.Values)
        {
            var existing = await db.Workouts.FindAsync([workout.Id], ct);
            if (existing is null)
            {
                db.Workouts.Add(workout);
            }
            else
            {
                db.Entry(existing).CurrentValues.SetValues(workout);
            }
        }

        await db.SaveChangesAsync(ct);

        foreach (var (workoutId, list) in splits)
        {
            var already = await db.WorkoutSplits.Where(s => s.WorkoutId == workoutId).Select(s => s.SplitIndex).ToListAsync(ct);
            var alreadySet = already.ToHashSet();
            db.WorkoutSplits.AddRange(list.Where(s => !alreadySet.Contains(s.SplitIndex)));
        }

        await db.SaveChangesAsync(ct);

        foreach (var record in records)
        {
            var exists = await db.PersonalRecords.AnyAsync(r => r.NameLocalizationId == record.NameLocalizationId && r.AchieveTimeUtc == record.AchieveTimeUtc, ct);
            if (!exists)
            {
                db.PersonalRecords.Add(record);
            }
        }

        await db.SaveChangesAsync(ct);
        context.RowsImported += workouts.Count + splits.Sum(s => s.Value.Count) + records.Count;

        context.ReportProgress("Sensordaten der Workouts werden gespeichert...", 92);
        await SaveSamplesAsync(context, samples, ct);
        context.ReportProgress("Workouts importiert", 100);
    }

    private async Task SaveSamplesAsync(ImportContext context, Dictionary<long, Dictionary<DateTime, WorkoutSample>> samples, CancellationToken ct)
    {
        await using var db = context.CreateContext();
        long total = 0;
        foreach (var (workoutId, byTime) in samples)
        {
            var existingTimes = (await db.WorkoutSamples.Where(s => s.WorkoutId == workoutId).Select(s => s.Timestamp).ToListAsync(ct)).ToHashSet();
            foreach (var sample in byTime.Values)
            {
                if (existingTimes.Add(sample.Timestamp))
                {
                    db.WorkoutSamples.Add(sample);
                    total++;
                }
            }
        }

        await db.SaveChangesAsync(ct);
        context.RowsImported += total;
    }

    // ---- UserExercises ----------------------------------------------------

    private static void ParseUserExercises(string folder, Dictionary<long, Workout> workouts, Dictionary<long, List<WorkoutSplit>> splits)
    {
        foreach (var file in Directory.EnumerateFiles(folder, "UserExercises_*.csv").OrderBy(f => f))
        {
            using var reader = new StreamReader(file);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            csv.Read();
            csv.ReadHeader();

            while (csv.Read())
            {
                if (!long.TryParse(csv.GetField("exercise_id"), out var id))
                {
                    continue;
                }

                var start = ParseUtc(csv.GetField("exercise_start"));
                var end = ParseUtc(csv.GetField("exercise_end"));
                if (start is null || end is null)
                {
                    continue;
                }

                var avgSpeedMmS = ParseNullableDouble(csv.GetField("tracker_avg_speed_mm_per_second"));
                var avgSpeedKmh = avgSpeedMmS is > 0 ? avgSpeedMmS * 0.0036 : null;
                var peakSpeedMmS = ParseNullableDouble(csv.GetField("tracker_peak_speed_mm_per_second"));

                var distanceMm = ParseNullableDouble(csv.GetField("tracker_total_distance_mm"));
                if (distanceMm is null or 0)
                {
                    distanceMm = ParseNullableDouble(csv.GetField("manually_logged_total_distance_mm"));
                }

                var calories = ParseNullableDouble(csv.GetField("tracker_total_calories"));
                if (calories is null or 0)
                {
                    calories = ParseNullableDouble(csv.GetField("manually_logged_total_calories"));
                }

                var steps = ParseNullableDouble(csv.GetField("tracker_total_steps"));
                if (steps is null or 0)
                {
                    steps = ParseNullableDouble(csv.GetField("manually_logged_total_steps"));
                }

                var workout = new Workout
                {
                    Id = id,
                    StartUtc = start.Value,
                    EndUtc = end.Value,
                    ActivityName = csv.GetField("activity_name") ?? "Workout",
                    LogType = csv.GetField("log_type") ?? "",
                    DistanceMeters = distanceMm is > 0 ? distanceMm / 1000.0 : null,
                    Calories = calories is > 0 ? calories : null,
                    Steps = steps is > 0 ? (int)steps : null,
                    AvgHeartRate = NullIfZero(ParseNullableDouble(csv.GetField("tracker_avg_heart_rate"))),
                    PeakHeartRate = NullIfZero(ParseNullableDouble(csv.GetField("tracker_peak_heart_rate"))),
                    AvgSpeedKmh = avgSpeedKmh,
                    PeakSpeedKmh = peakSpeedMmS is > 0 ? peakSpeedMmS * 0.0036 : null,
                    AvgPaceSecPerKm = avgSpeedKmh is > 0 ? 3600.0 / avgSpeedKmh : null,
                    ElevationGainMeters = NullIfZero(ParseNullableDouble(csv.GetField("tracker_total_altitude_mm")) / 1000.0),
                    CardioLoad = ParseNullableDouble(csv.GetField("tracker_cardio_load")),
                    HasGps = false,
                };

                if (workout.AvgSpeedKmh is null && workout.DistanceMeters is > 0 && workout.DurationSeconds > 0)
                {
                    workout.AvgSpeedKmh = (workout.DistanceMeters.Value / 1000.0) / (workout.DurationSeconds / 3600.0);
                    workout.AvgPaceSecPerKm = 3600.0 / workout.AvgSpeedKmh;
                }

                workouts[id] = workout;
                splits[id] = ParseEvents(csv.GetField("events"), id);
            }
        }
    }

    [GeneratedRegex(@"\[([^\]]*)\]")]
    private static partial Regex EventGroupRegex();

    private static List<WorkoutSplit> ParseEvents(string? raw, long workoutId)
    {
        var result = new List<WorkoutSplit>();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return result;
        }

        var idx = 0;
        foreach (Match group in EventGroupRegex().Matches(raw))
        {
            var fields = new Dictionary<string, string>();
            foreach (var token in group.Groups[1].Value.Split(", "))
            {
                var sep = token.IndexOf(": ", StringComparison.Ordinal);
                if (sep < 0)
                {
                    continue;
                }

                fields[token[..sep].Trim()] = token[(sep + 2)..].Trim();
            }

            if (!fields.TryGetValue("Type", out var type) || string.IsNullOrEmpty(type))
            {
                continue;
            }

            var speedMmS = ParseNullableDouble(fields.GetValueOrDefault("AverageSpeedMmPerSec"));

            result.Add(new WorkoutSplit
            {
                WorkoutId = workoutId,
                SplitIndex = idx++,
                Type = type,
                Timestamp = ParseUtc(fields.GetValueOrDefault("Timestamp")) ?? default,
                ElapsedMs = (long)(ParseNullableDouble(fields.GetValueOrDefault("ElapsedTimeMillis")) ?? 0),
                DistanceMeters = (ParseNullableDouble(fields.GetValueOrDefault("TraveledDistanceMm")) ?? 0) / 1000.0,
                Calories = ParseNullableDouble(fields.GetValueOrDefault("CaloriesBurned")) ?? 0,
                Steps = (int)(ParseNullableDouble(fields.GetValueOrDefault("Steps")) ?? 0),
                AvgHeartRate = NullIfZero(ParseNullableDouble(fields.GetValueOrDefault("AverageHeartRate"))),
                ElevationGainMeters = (ParseNullableDouble(fields.GetValueOrDefault("ElevationGainMm")) ?? 0) / 1000.0,
                AvgSpeedKmh = speedMmS is > 0 ? speedMmS * 0.0036 : null,
            });
        }

        return result;
    }

    // ---- WorkoutSummariesAndRounds (running dynamics) ----------------------

    private static void ParseWorkoutSummariesAndRounds(string folder, Dictionary<long, Workout> workouts)
    {
        var path = Path.Combine(folder, "WorkoutSummariesAndRounds.csv");
        if (!File.Exists(path))
        {
            return;
        }

        var intervals = new Dictionary<(DateTime start, DateTime end), (double? cadence, double? vertOsc, double? vertRatio, double? gct, double? rpe)>();

        using (var reader = new StreamReader(path))
        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
        {
            csv.Read();
            csv.ReadHeader();
            while (csv.Read())
            {
                var start = ParseUtc(csv.GetField("interval_start"));
                var end = ParseUtc(csv.GetField("interval_end"));
                if (start is null || end is null)
                {
                    continue;
                }

                var key = (start.Value, end.Value);
                if (!intervals.ContainsKey(key))
                {
                    intervals[key] = (
                        ParseNullableDouble(csv.GetField("avg_cadence_spm")),
                        ParseNullableDouble(csv.GetField("avg_vertical_oscillation_mm")),
                        ParseNullableDouble(csv.GetField("avg_vertical_ratio")),
                        ParseNullableDouble(csv.GetField("avg_ground_contact_time_duration_micro_sec")) is { } us ? us / 1000.0 : null,
                        ParseNullableDouble(csv.GetField("rate_perceived_exertion")));
                }
            }
        }

        foreach (var workout in workouts.Values)
        {
            var match = intervals.FirstOrDefault(kv =>
                Math.Abs((kv.Key.start - workout.StartUtc).TotalMinutes) <= 3 &&
                Math.Abs((kv.Key.end - workout.EndUtc).TotalMinutes) <= 3);

            if (match.Key == default)
            {
                continue;
            }

            workout.CadenceAvgSpm = match.Value.cadence;
            workout.VerticalOscillationMm = match.Value.vertOsc;
            workout.VerticalRatioPercent = match.Value.vertRatio;
            workout.GroundContactTimeMs = match.Value.gct;
            workout.RatePerceivedExertion = match.Value.rpe;
        }
    }

    // ---- PersonalRecords ----------------------------------------------------

    private static void ParsePersonalRecords(string folder, Dictionary<long, Workout> workouts, List<PersonalRecord> records)
    {
        var path = Path.Combine(folder, "PersonalRecords.csv");
        if (!File.Exists(path))
        {
            return;
        }

        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        csv.Read();
        csv.ReadHeader();
        while (csv.Read())
        {
            var achieve = ParseUtc(csv.GetField("achieve_time"));
            if (achieve is null)
            {
                continue;
            }

            var exerciseId = long.TryParse(csv.GetField("exercise_id"), out var eid) ? eid : (long?)null;
            var extentRaw = csv.GetField("extent_value");
            double? extentMeters = extentRaw is not null && extentRaw != "N/A" && double.TryParse(extentRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var extentMm)
                ? extentMm / 1000.0
                : null;

            records.Add(new PersonalRecord
            {
                WorkoutId = exerciseId is not null && workouts.ContainsKey(exerciseId.Value) ? exerciseId : null,
                NameLocalizationId = csv.GetField("name_localization_id") ?? "",
                State = csv.GetField("state") ?? "",
                AchieveTimeUtc = achieve.Value,
                RecordValue = ParseNullableDouble(csv.GetField("record_value")) ?? 0,
                RecordType = csv.GetField("personal_record_type") ?? "",
                ExtentValueMeters = extentMeters,
            });
        }
    }

    // ---- helpers --------------------------------------------------------

    private static double? ParseNullableDouble(string? s) =>
        !string.IsNullOrWhiteSpace(s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static double? NullIfZero(double? v) => v is null or 0 ? null : v;

    private static DateTime? ParseUtc(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt)
            ? dt
            : null;
    }
}
