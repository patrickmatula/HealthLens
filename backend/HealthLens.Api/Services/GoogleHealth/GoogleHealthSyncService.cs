using HealthLens.Api.Data;
using HealthLens.Api.Dtos;
using HealthLens.Api.Models;

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
/// earlier guesses assumed. Resting heart rate, sleep stages and workouts are left for a follow-up.
/// </summary>
public sealed class GoogleHealthSyncService(
    GoogleHealthCredentialStore store,
    GoogleHealthOAuthService oauth,
    GoogleHealthApiClient api,
    DataSessionService session,
    ILogger<GoogleHealthSyncService> logger)
{
    private const int SyncWindowDays = 30;

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

        await db.SaveChangesAsync(ct);
        session.MarkHasData();

        creds.LastSyncUtc = DateTime.UtcNow;
        creds.LastSyncSummary = $"{activityDays} Tage Aktivität, {weightEntries} Gewichts-/Körperfettwerte";
        creds.LastError = null;
        store.Save(creds);

        return new GoogleHealthSyncResultDto(activityDays, 0, weightEntries, warnings);
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
        string accessToken, string dataTypeId, string unionField, bool isSampleType, DateTimeOffset from, DateTimeOffset to, List<string> warnings, CancellationToken ct)
    {
        try
        {
            return await api.ListDataPointsAsync(accessToken, dataTypeId, unionField, isSampleType, from, to, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Google Health sync: fetching '{DataType}' failed", dataTypeId);
            warnings.Add($"{dataTypeId} konnte nicht abgerufen werden: {ex.Message}");
            return [];
        }
    }
}
