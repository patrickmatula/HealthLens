using HealthLens.Api.Data;
using HealthLens.Api.Dtos;
using HealthLens.Api.Models;

namespace HealthLens.Api.Services.GoogleHealth;

/// <summary>
/// Pulls the last month of a few core, unambiguous data types from the Google Health API and upserts
/// them into the same tables the Takeout importer writes to — so every existing chart/gauge just sees
/// fresher data, with no separate "synced" code path in the frontend.
///
/// Deliberately narrow: only data types whose Google Health API field names are unambiguous (steps,
/// distance, active-energy-burned, weight, body-fat) are synced. Resting heart rate, sleep stages and
/// workouts are left for a follow-up once a real account has confirmed the exact response shape — this
/// app has no way to test against the live API without the user's own OAuth credentials, and getting a
/// medically-labeled gauge (resting heart rate) or a hypnogram wrong from a guessed field mapping would
/// be worse than not syncing it at all.
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
        var stepsByDay = await SumPerDayAsync(accessToken, "steps", "steps", ["count", "value"], from, to, warnings, ct);
        var distanceByDay = await SumPerDayAsync(accessToken, "distance", "distance", ["meters", "value"], from, to, warnings, ct);
        var caloriesByDay = await SumPerDayAsync(accessToken, "active-energy-burned", "activeEnergyBurned", ["kilocalories", "value"], from, to, warnings, ct);

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
        count += await UpsertBodyMeasurementAsync(db, accessToken, "weight", "weight", ["kilograms", "value"], BodyMeasurementType.WeightKg, from, to, warnings, ct);
        count += await UpsertBodyMeasurementAsync(db, accessToken, "body-fat", "bodyFat", ["percentage", "value"], BodyMeasurementType.BodyFatPercent, from, to, warnings, ct);
        return count;
    }

    private async Task<int> UpsertBodyMeasurementAsync(
        AppDbContext db, string accessToken, string dataTypeId, string unionField, string[] candidates,
        BodyMeasurementType type, DateTimeOffset from, DateTimeOffset to, List<string> warnings, CancellationToken ct)
    {
        var points = await FetchAsync(accessToken, dataTypeId, from, to, warnings, ct);
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

            var existing = await db.BodyMeasurements.FindAsync([group.Key, type, BodySide.None], ct);
            if (existing is null)
            {
                db.BodyMeasurements.Add(new BodyMeasurement { Date = group.Key, Type = type, Side = BodySide.None, Value = value.Value });
            }
            else
            {
                existing.Value = value.Value;
            }

            count++;
        }

        return count;
    }

    private async Task<Dictionary<DateOnly, double>> SumPerDayAsync(
        string accessToken, string dataTypeId, string unionField, string[] candidates,
        DateTimeOffset from, DateTimeOffset to, List<string> warnings, CancellationToken ct)
    {
        var points = await FetchAsync(accessToken, dataTypeId, from, to, warnings, ct);
        var byDay = new Dictionary<DateOnly, double>();

        foreach (var point in points)
        {
            var value = GoogleHealthFieldReader.ReadNumeric(point.Raw, unionField, logger, candidates);
            if (value is null)
            {
                continue;
            }

            var day = DateOnly.FromDateTime(point.StartUtc.UtcDateTime);
            byDay[day] = byDay.GetValueOrDefault(day) + value.Value;
        }

        return byDay;
    }

    private async Task<IReadOnlyList<GoogleHealthDataPoint>> FetchAsync(
        string accessToken, string dataTypeId, DateTimeOffset from, DateTimeOffset to, List<string> warnings, CancellationToken ct)
    {
        try
        {
            return await api.ListDataPointsAsync(accessToken, dataTypeId, from, to, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Google Health sync: fetching '{DataType}' failed", dataTypeId);
            warnings.Add($"{dataTypeId} konnte nicht abgerufen werden: {ex.Message}");
            return [];
        }
    }
}
