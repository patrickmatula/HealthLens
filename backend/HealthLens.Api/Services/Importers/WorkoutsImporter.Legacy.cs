using System.Globalization;
using System.Text.Json;
using HealthLens.Api.Models;
using HealthLens.Api.Services.Import;

namespace HealthLens.Api.Services.Importers;

public partial class WorkoutsImporter
{
    private static readonly TimeSpan MatchBuffer = TimeSpan.FromMinutes(5);

    /// <summary>Gap-fills workouts from the legacy Fitbit exercise-*.json export, skipping any log that already overlaps a modern (UserExercises) workout.</summary>
    private static void ImportLegacyExercises(string folder, Dictionary<long, Workout> workouts)
    {
        var modern = new IntervalIndex();
        foreach (var w in workouts.Values)
        {
            modern.Add(w.StartUtc, w.EndUtc);
        }

        foreach (var file in Directory.EnumerateFiles(folder, "exercise-*.json"))
        {
            using var stream = File.OpenRead(file);
            using var doc = JsonDocument.Parse(stream);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var entry in doc.RootElement.EnumerateArray())
            {
                if (!entry.TryGetProperty("logId", out var idProp))
                {
                    continue;
                }

                var id = idProp.GetInt64();
                if (workouts.ContainsKey(id))
                {
                    continue;
                }

                if (ParseLegacyLocalTime(LegacyJson.String(entry, "startTime")) is not { } start)
                {
                    continue;
                }

                var end = start.AddMilliseconds(LegacyJson.Number(entry, "duration") ?? 0);
                if (modern.Overlaps(start, end))
                {
                    continue;
                }

                var distanceKm = LegacyJson.Number(entry, "distance");
                var avgHr = LegacyJson.Number(entry, "averageHeartRate");
                var speedKmh = LegacyJson.Number(entry, "speed");

                workouts[id] = new Workout
                {
                    Id = id,
                    StartUtc = start,
                    EndUtc = end,
                    ActivityName = LegacyJson.String(entry, "activityName") ?? "Workout",
                    LogType = LegacyJson.String(entry, "logType") ?? "",
                    Source = "Legacy Export",
                    DistanceMeters = distanceKm is > 0 ? distanceKm * 1000 : null,
                    Calories = LegacyJson.Number(entry, "calories"),
                    Steps = (int?)LegacyJson.Number(entry, "steps"),
                    AvgHeartRate = NullIfZero(avgHr),
                    AvgSpeedKmh = speedKmh,
                    AvgPaceSecPerKm = speedKmh is > 0 ? 3600.0 / speedKmh : null,
                    ElevationGainMeters = NullIfZero(LegacyJson.Number(entry, "elevationGain")),
                    HasGps = LegacyJson.Bool(entry, "hasGps"),
                    IsLegacy = true,
                };
            }
        }
    }

    private static DateTime? ParseLegacyLocalTime(string? raw) =>
        !string.IsNullOrWhiteSpace(raw) &&
        DateTime.TryParseExact(raw, "MM/dd/yy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)
            ? LocalTimeZone.ToUtc(local)
            : null;
}
