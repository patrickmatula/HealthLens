using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HealthLens.Api.Data;
using HealthLens.Api.Dtos;
using HealthLens.Api.Models;
using HealthLens.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace HealthLens.Api.Services.GoogleHealth;

/// <summary>
/// Pulls the last month of a few core data types from the Google Health API and upserts them into the
/// same tables the Takeout importer writes to — so every existing chart/gauge just sees fresher data,
/// with no separate "synced" code path in the frontend.
///
/// Field names and nesting are taken from the official REST reference
/// (developers.google.com/health/reference/rest/v4/users.dataTypes.dataPoints): every field lives nested
/// under the data type's own union object, e.g. a "distance" data point is
/// { "distance": { "interval": {...}, "millimeters": "1000000" } }, not a top-level "meters"/"value" as
/// earlier guesses assumed. Resting heart rate and sleep stages are left for a follow-up.
/// </summary>
public sealed class GoogleHealthSyncService(
    GoogleHealthCredentialStore store,
    GoogleHealthOAuthService oauth,
    GoogleHealthApiClient api,
    DataSessionService session,
    ShoeDefaultsStore shoeDefaults,
    ILogger<GoogleHealthSyncService> logger)
{
    private const int SyncWindowDays = 30;
    private const string GoogleHealthWorkoutSource = "Google Health";

    public async Task<GoogleHealthSyncResultDto> SyncAsync(CancellationToken ct)
    {
        var creds = store.Load();
        var accessToken = await oauth.EnsureFreshAccessTokenAsync(creds, ct);

        var to = DateTimeOffset.UtcNow;
        var from = to.AddDays(-SyncWindowDays);
        var warnings = new List<string>();

        await using var db = session.CreateContext();

        var activityDays = await SyncActivityAsync(db, accessToken, from, to, warnings, ct);
        var weightEntries = await SyncWeightAsync(db, accessToken, from, to, warnings, ct);
        var workoutsSynced = await SyncWorkoutsAsync(db, accessToken, from, to, warnings, ct);

        await db.SaveChangesAsync(ct);
        session.MarkHasData();

        creds.LastSyncUtc = DateTime.UtcNow;
        creds.LastSyncSummary = $"{workoutsSynced} Workouts, {activityDays} Tage Aktivität, {weightEntries} Gewichts-/Körperfettwerte";
        creds.LastError = null;
        store.Save(creds);

        return new GoogleHealthSyncResultDto(activityDays, 0, weightEntries, workoutsSynced, warnings);
    }

    private async Task<int> SyncActivityAsync(AppDbContext db, string accessToken, DateTimeOffset from, DateTimeOffset to, List<string> warnings, CancellationToken ct)
    {
        // "count" and "millimeters" are int64 fields, JSON-encoded as strings by the API.
        var stepsByDay = await SumPerDayAsync(accessToken, "steps", "steps", ["count"], unitDivisor: 1, from, to, warnings, ct);
        var distanceByDay = await SumPerDayAsync(accessToken, "distance", "distance", ["millimeters"], unitDivisor: 1000, from, to, warnings, ct);
        var caloriesByDay = await SumPerDayAsync(accessToken, "active-energy-burned", "activeEnergyBurned", ["caloriesKcal"], unitDivisor: 1, from, to, warnings, ct);

        var days = new HashSet<DateOnly>(stepsByDay.Keys);
        days.UnionWith(distanceByDay.Keys);
        days.UnionWith(caloriesByDay.Keys);

        foreach (var day in days)
        {
            var existing = await db.DailyActivitySummaries.FindAsync([day], ct);
            if (existing is null)
            {
                existing = new DailyActivitySummary { Date = day };
                db.DailyActivitySummaries.Add(existing);
            }

            if (stepsByDay.TryGetValue(day, out var steps))
            {
                existing.Steps = (int)Math.Round(steps);
            }

            if (distanceByDay.TryGetValue(day, out var distance))
            {
                existing.DistanceMeters = distance;
            }

            if (caloriesByDay.TryGetValue(day, out var calories))
            {
                existing.CaloriesActive = calories;
            }
        }

        return days.Count;
    }

    private async Task<int> SyncWeightAsync(AppDbContext db, string accessToken, DateTimeOffset from, DateTimeOffset to, List<string> warnings, CancellationToken ct)
    {
        var count = 0;
        // "weightGrams" is grams, converted to the app's kilogram convention; "percentage" needs no conversion.
        count += await UpsertBodyMeasurementAsync(db, accessToken, "weight", "weight", ["weightGrams"], unitDivisor: 1000, BodyMeasurementType.WeightKg, from, to, warnings, ct);
        count += await UpsertBodyMeasurementAsync(db, accessToken, "body-fat", "bodyFat", ["percentage"], unitDivisor: 1, BodyMeasurementType.BodyFatPercent, from, to, warnings, ct);
        return count;
    }

    private async Task<int> UpsertBodyMeasurementAsync(
        AppDbContext db, string accessToken, string dataTypeId, string unionField, string[] candidates, double unitDivisor,
        BodyMeasurementType type, DateTimeOffset from, DateTimeOffset to, List<string> warnings, CancellationToken ct)
    {
        var points = await FetchAsync(accessToken, dataTypeId, unionField, isSampleType: true, from, to, warnings, ct);
        var count = 0;

        // Last measurement of each day wins, matching the "re-entering a value overwrites it" convention
        // already used for manual entries.
        foreach (var group in points.GroupBy(p => DateOnly.FromDateTime(p.StartUtc.UtcDateTime)))
        {
            var latest = group.OrderBy(p => p.StartUtc).Last();
            var value = GoogleHealthFieldReader.ReadNumeric(latest.Raw, unionField, logger, candidates);
            if (value is null)
            {
                continue;
            }

            var converted = value.Value / unitDivisor;
            var existing = await db.BodyMeasurements.FindAsync([group.Key, type, BodySide.None], ct);
            if (existing is null)
            {
                db.BodyMeasurements.Add(new BodyMeasurement { Date = group.Key, Type = type, Side = BodySide.None, Value = converted });
            }
            else
            {
                existing.Value = converted;
            }

            count++;
        }

        return count;
    }

    /// <summary>
    /// Syncs workout sessions from the "exercise" data type (a Session-type record, distinct from the
    /// interval/sample types above -- but it exposes the same interval.startTime/endTime shape, so the
    /// existing interval fetch path works unchanged). Google Health's exercise sessions don't carry GPS
    /// route points, per-second streams, or split data (those live in separate data types this app
    /// doesn't sync), so a synced workout only ever gets the summary fields: distance, duration, calories,
    /// average heart rate/pace/speed, elevation gain.
    ///
    /// Re-derives this sync window from scratch every run (delete all previously-synced workouts in
    /// [from, to], then reinsert) rather than upserting by a stable id, since Google Health's own point
    /// identifiers have no relationship to Fitbit's exercise_id the rest of the schema is keyed on. This
    /// self-heals if a Takeout import later supersedes a synced entry for the same time range: the next
    /// sync deletes the stale synced row and the overlap check below skips recreating it, since a
    /// non-synced (Takeout-sourced) workout now already occupies that slot.
    /// </summary>
    private async Task<int> SyncWorkoutsAsync(AppDbContext db, string accessToken, DateTimeOffset from, DateTimeOffset to, List<string> warnings, CancellationToken ct)
    {
        var points = await FetchAsync(accessToken, "exercise", "exercise", isSampleType: false, from, to, warnings, ct, useCivilDateFilter: true);

        var previouslySynced = await db.Workouts
            .Where(w => w.Source == GoogleHealthWorkoutSource && w.StartUtc >= from.UtcDateTime && w.StartUtc < to.UtcDateTime)
            .ToListAsync(ct);
        // The synthetic id is a stable hash of Google's own data point identifier, so the same workout
        // gets the same id on every sync -- carry over ShoeId (the only field a user can edit on a synced
        // workout) by id before deleting, or re-assigning a shoe would get silently wiped on next sync.
        var previousShoeIds = previouslySynced.Where(w => w.ShoeId != null).ToDictionary(w => w.Id, w => w.ShoeId);
        db.Workouts.RemoveRange(previouslySynced);

        var others = await db.Workouts
            .Where(w => w.Source != GoogleHealthWorkoutSource)
            .Select(w => new { w.StartUtc, w.EndUtc })
            .ToListAsync(ct);

        var count = 0;
        foreach (var point in points)
        {
            if (!point.Raw.TryGetProperty("exercise", out var union))
            {
                continue;
            }

            var startUtc = point.StartUtc.UtcDateTime;
            var endUtc = point.EndUtc.UtcDateTime;

            // Prefer already-imported (Takeout) data for any time range it covers -- that data has GPS,
            // splits, and per-second streams a synced entry never will.
            if (others.Any(o => startUtc < o.EndUtc && o.StartUtc < endUtc))
            {
                continue;
            }

            var exerciseType = union.TryGetProperty("exerciseType", out var typeProp) ? typeProp.GetString() : null;
            var metrics = union.TryGetProperty("metricsSummary", out var m) ? m : default;
            var hasGps = union.TryGetProperty("exerciseMetadata", out var meta)
                && meta.TryGetProperty("hasGps", out var gpsProp)
                && gpsProp.ValueKind == JsonValueKind.True;

            var distanceMm = GoogleHealthFieldReader.ReadNumericField(metrics, "distanceMillimeters");
            var elevationMm = GoogleHealthFieldReader.ReadNumericField(metrics, "elevationGainMillimeters");
            var avgPaceSecPerM = GoogleHealthFieldReader.ReadNumericField(metrics, "averagePaceSecondsPerMeter");
            var avgSpeedMmPerSec = GoogleHealthFieldReader.ReadNumericField(metrics, "averageSpeedMillimetersPerSecond");
            var steps = GoogleHealthFieldReader.ReadNumericField(metrics, "steps");

            var identifier = point.Raw.TryGetProperty("dataPointName", out var dpn) ? dpn.GetString()
                : point.Raw.TryGetProperty("name", out var np) ? np.GetString()
                : null;
            identifier ??= $"{point.StartUtc:O}|{exerciseType}";
            var id = StableWorkoutId(identifier);
            var activityName = HumanizeExerciseType(exerciseType);
            var avgPaceSecPerKm = avgPaceSecPerM * 1000;

            // Only fall back to the category default when this exact synced workout never had a manual
            // shoe assignment to preserve — otherwise every sync would silently reset a user's override.
            var shoeId = previousShoeIds.GetValueOrDefault(id)
                ?? shoeDefaults.GetDefaultShoeId(WorkoutCategorizer.Categorize(activityName, avgPaceSecPerKm, null));

            db.Workouts.Add(new Workout
            {
                Id = id,
                StartUtc = startUtc,
                EndUtc = endUtc,
                ActivityName = activityName,
                LogType = "TRACKER",
                Source = GoogleHealthWorkoutSource,
                DistanceMeters = distanceMm / 1000,
                Calories = GoogleHealthFieldReader.ReadNumericField(metrics, "caloriesKcal"),
                Steps = steps is { } s ? (int)Math.Round(s) : null,
                AvgHeartRate = GoogleHealthFieldReader.ReadNumericField(metrics, "averageHeartRateBeatsPerMinute"),
                AvgPaceSecPerKm = avgPaceSecPerKm,
                AvgSpeedKmh = avgSpeedMmPerSec * 0.0036,
                ElevationGainMeters = elevationMm / 1000,
                HasGps = hasGps,
                IsLegacy = false,
                ShoeId = shoeId,
            });
            count++;
        }

        return count;
    }

    /// <summary>Converts a SCREAMING_SNAKE_CASE exerciseType enum value (e.g. "STRENGTH_TRAINING") into a
    /// readable name ("Strength Training") -- deliberately generic rather than a lookup table, since the
    /// API defines 200+ exercise types. This also happens to satisfy the app's existing keyword-based
    /// run/walk/bike categorization (utils/format.ts categorizeWorkout) for the common cases (e.g.
    /// "Running" contains "run") without needing exerciseType-specific handling there.</summary>
    private static string HumanizeExerciseType(string? exerciseType)
    {
        if (string.IsNullOrEmpty(exerciseType))
        {
            return "Workout";
        }

        var words = exerciseType.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words.Select(w => char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()));
    }

    /// <summary>Derives a stable 63-bit positive id from a Google Health data point identifier, for a
    /// synced Workout row's primary key. Collision risk against Fitbit's own 64-bit exercise_id values or
    /// between two synced workouts is negligible at this app's single-user data volume.</summary>
    private static long StableWorkoutId(string identifier)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identifier));
        var value = BitConverter.ToInt64(hash, 0);
        return value == long.MinValue ? long.MaxValue : Math.Abs(value);
    }

    private async Task<Dictionary<DateOnly, double>> SumPerDayAsync(
        string accessToken, string dataTypeId, string unionField, string[] candidates, double unitDivisor,
        DateTimeOffset from, DateTimeOffset to, List<string> warnings, CancellationToken ct)
    {
        var points = await FetchAsync(accessToken, dataTypeId, unionField, isSampleType: false, from, to, warnings, ct);
        var byDay = new Dictionary<DateOnly, double>();

        foreach (var point in points)
        {
            var value = GoogleHealthFieldReader.ReadNumeric(point.Raw, unionField, logger, candidates);
            if (value is null)
            {
                continue;
            }

            var day = DateOnly.FromDateTime(point.StartUtc.UtcDateTime);
            byDay[day] = byDay.GetValueOrDefault(day) + value.Value / unitDivisor;
        }

        return byDay;
    }

    private async Task<IReadOnlyList<GoogleHealthDataPoint>> FetchAsync(
        string accessToken, string dataTypeId, string unionField, bool isSampleType, DateTimeOffset from, DateTimeOffset to, List<string> warnings, CancellationToken ct, bool useCivilDateFilter = false)
    {
        try
        {
            return await api.ListDataPointsAsync(accessToken, dataTypeId, unionField, isSampleType, from, to, ct, useCivilDateFilter);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Google Health sync: fetching '{DataType}' failed", dataTypeId);
            warnings.Add($"{dataTypeId} konnte nicht abgerufen werden: {ex.Message}");
            return [];
        }
    }
}
