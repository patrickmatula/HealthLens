using System.Globalization;
using System.Text.RegularExpressions;
using HealthLens.Api.Models;
using HealthLens.Api.Services.Csv;
using HealthLens.Api.Services.Import;
using HealthLens.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace HealthLens.Api.Services.Importers;

/// <summary>
/// Builds the Workouts / WorkoutSplits / WorkoutSamples / PersonalRecords tables from three
/// modern sources (UserExercises, WorkoutSummariesAndRounds, PersonalRecords) plus a time-range
/// join against the per-second running-dynamics/GPS/heart-rate streams, then gap-fills pre-2025
/// history from the legacy Fitbit exercise-*.json export.
/// </summary>
public partial class WorkoutsImporter(ShoeDefaultsStore? shoeDefaults = null) : IDomainImporter
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
            await ParseUserExercisesAsync(hfd, workouts, splits, ct);

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
                workout.ShoeId = shoeDefaults?.GetDefaultShoeId(WorkoutCategorizer.Categorize(workout.ActivityName, workout.AvgPaceSecPerKm, workout.CadenceAvgSpm));
                db.Workouts.Add(workout);
            }
            else
            {
                // CurrentValues.SetValues copies every scalar property from the freshly-parsed `workout`,
                // which never carries a ShoeId (that's only ever set by the user or the default-shoe logic
                // above) — without preserving it here, every re-import would silently wipe every shoe
                // assignment. Same bug class already fixed for GoogleHealthSyncService's resync path.
                var preservedShoeId = existing.ShoeId;
                db.Entry(existing).CurrentValues.SetValues(workout);
                existing.ShoeId = preservedShoeId;
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
        context.AddRows(workouts.Count + splits.Sum(s => s.Value.Count) + records.Count);

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
        context.AddRows(total);
    }

    // ---- UserExercises ----------------------------------------------------

    private readonly record struct UserExercisesResult(Dictionary<long, Workout> Workouts, Dictionary<long, List<WorkoutSplit>> Splits);

    private static async Task ParseUserExercisesAsync(string folder, Dictionary<long, Workout> workouts, Dictionary<long, List<WorkoutSplit>> splits, CancellationToken ct)
    {
        var files = Directory.EnumerateFiles(folder, "UserExercises_*.csv").Order(StringComparer.Ordinal).ToArray();

        await ParallelCsv.RunAsync(
            files,
            ParseUserExercisesFile,
            partial =>
            {
                foreach (var (id, workout) in partial.Workouts)
                {
                    workouts[id] = workout;
                }

                foreach (var (id, list) in partial.Splits)
                {
                    splits[id] = list;
                }
            },
            null,
            ct);
    }

    private static UserExercisesResult ParseUserExercisesFile(string file)
    {
        var workouts = new Dictionary<long, Workout>();
        var splits = new Dictionary<long, List<WorkoutSplit>>();

        using var csv = CsvCursor.Open(file);
        if (csv is null)
        {
            return new UserExercisesResult(workouts, splits);
        }

        var id = csv.Column("exercise_id");
        var start = csv.Column("exercise_start");
        var end = csv.Column("exercise_end");
        var avgSpeedCol = csv.Column("tracker_avg_speed_mm_per_second");
        var peakSpeedCol = csv.Column("tracker_peak_speed_mm_per_second");
        var trackerDistance = csv.Column("tracker_total_distance_mm");
        var manualDistance = csv.Column("manually_logged_total_distance_mm");
        var trackerCalories = csv.Column("tracker_total_calories");
        var manualCalories = csv.Column("manually_logged_total_calories");
        var trackerSteps = csv.Column("tracker_total_steps");
        var manualSteps = csv.Column("manually_logged_total_steps");
        var activityName = csv.Column("activity_name");
        var logType = csv.Column("log_type");
        var avgHr = csv.Column("tracker_avg_heart_rate");
        var peakHr = csv.Column("tracker_peak_heart_rate");
        var altitude = csv.Column("tracker_total_altitude_mm");
        var cardioLoad = csv.Column("tracker_cardio_load");
        var events = csv.Column("events");

        while (csv.NextRow())
        {
            if (csv.GetInt64(id) is not { } exerciseId || csv.GetUtc(start) is not { } startUtc || csv.GetUtc(end) is not { } endUtc)
            {
                continue;
            }

            var avgSpeedMmS = csv.GetDouble(avgSpeedCol);
            var avgSpeedKmh = avgSpeedMmS is > 0 ? avgSpeedMmS * 0.0036 : null;
            var peakSpeedMmS = csv.GetDouble(peakSpeedCol);

            var distanceMm = csv.GetDouble(trackerDistance);
            if (distanceMm is null or 0)
            {
                distanceMm = csv.GetDouble(manualDistance);
            }

            var calories = csv.GetDouble(trackerCalories);
            if (calories is null or 0)
            {
                calories = csv.GetDouble(manualCalories);
            }

            var steps = csv.GetDouble(trackerSteps);
            if (steps is null or 0)
            {
                steps = csv.GetDouble(manualSteps);
            }

            var workout = new Workout
            {
                Id = exerciseId,
                StartUtc = startUtc,
                EndUtc = endUtc,
                ActivityName = csv.GetString(activityName) is { Length: > 0 } name ? name : "Workout",
                LogType = csv.GetString(logType),
                DistanceMeters = distanceMm is > 0 ? distanceMm / 1000.0 : null,
                Calories = calories is > 0 ? calories : null,
                Steps = steps is > 0 ? (int)steps : null,
                AvgHeartRate = NullIfZero(csv.GetDouble(avgHr)),
                PeakHeartRate = NullIfZero(csv.GetDouble(peakHr)),
                AvgSpeedKmh = avgSpeedKmh,
                PeakSpeedKmh = peakSpeedMmS is > 0 ? peakSpeedMmS * 0.0036 : null,
                AvgPaceSecPerKm = avgSpeedKmh is > 0 ? 3600.0 / avgSpeedKmh : null,
                ElevationGainMeters = NullIfZero(csv.GetDouble(altitude) / 1000.0),
                CardioLoad = csv.GetDouble(cardioLoad),
                HasGps = false,
            };

            if (workout.AvgSpeedKmh is null && workout.DistanceMeters is > 0 && workout.DurationSeconds > 0)
            {
                workout.AvgSpeedKmh = (workout.DistanceMeters.Value / 1000.0) / (workout.DurationSeconds / 3600.0);
                workout.AvgPaceSecPerKm = 3600.0 / workout.AvgSpeedKmh;
            }

            workouts[exerciseId] = workout;
            splits[exerciseId] = ParseEvents(csv.GetString(events), exerciseId);
        }

        return new UserExercisesResult(workouts, splits);
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
        using var csv = CsvCursor.Open(path);
        if (csv is null)
        {
            return;
        }

        var startCol = csv.Column("interval_start");
        var endCol = csv.Column("interval_end");
        var cadenceCol = csv.Column("avg_cadence_spm");
        var vertOscCol = csv.Column("avg_vertical_oscillation_mm");
        var vertRatioCol = csv.Column("avg_vertical_ratio");
        var gctCol = csv.Column("avg_ground_contact_time_duration_micro_sec");
        var rpeCol = csv.Column("rate_perceived_exertion");

        var intervals = new Dictionary<(DateTime start, DateTime end), (double? cadence, double? vertOsc, double? vertRatio, double? gct, double? rpe)>();

        while (csv.NextRow())
        {
            if (csv.GetUtc(startCol) is not { } start || csv.GetUtc(endCol) is not { } end)
            {
                continue;
            }

            var key = (start, end);
            if (!intervals.ContainsKey(key))
            {
                intervals[key] = (
                    csv.GetDouble(cadenceCol),
                    csv.GetDouble(vertOscCol),
                    csv.GetDouble(vertRatioCol),
                    csv.GetDouble(gctCol) is { } us ? us / 1000.0 : null,
                    csv.GetDouble(rpeCol));
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
        using var csv = CsvCursor.Open(path);
        if (csv is null)
        {
            return;
        }

        var achieveCol = csv.Column("achieve_time");
        var exerciseIdCol = csv.Column("exercise_id");
        var extentCol = csv.Column("extent_value");
        var nameCol = csv.Column("name_localization_id");
        var stateCol = csv.Column("state");
        var valueCol = csv.Column("record_value");
        var typeCol = csv.Column("personal_record_type");

        while (csv.NextRow())
        {
            if (csv.GetUtc(achieveCol) is not { } achieve)
            {
                continue;
            }

            var exerciseId = csv.GetInt64(exerciseIdCol);
            var extentField = csv.Field(extentCol);
            double? extentMeters = !extentField.IsEmpty && !extentField.SequenceEqual("N/A") && csv.TryGetDouble(extentCol, out var extentMm)
                ? extentMm / 1000.0
                : null;

            records.Add(new PersonalRecord
            {
                WorkoutId = exerciseId is not null && workouts.ContainsKey(exerciseId.Value) ? exerciseId : null,
                NameLocalizationId = csv.GetString(nameCol),
                State = csv.GetString(stateCol),
                AchieveTimeUtc = achieve,
                RecordValue = csv.GetDoubleOrZero(valueCol),
                RecordType = csv.GetString(typeCol),
                ExtentValueMeters = extentMeters,
            });
        }
    }

    // ---- helpers --------------------------------------------------------

    private static double? ParseNullableDouble(string? s) =>
        !string.IsNullOrWhiteSpace(s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static double? NullIfZero(double? v) => v is null or 0 ? null : v;

    private static DateTime? ParseUtc(string? raw) => Timestamps.TryParseUtc(raw, out var value) ? value : null;
}
