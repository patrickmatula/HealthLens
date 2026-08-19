using System.Globalization;
using System.Text.Json;
using GoogleHealthLens.Api.Models;

namespace GoogleHealthLens.Api.Services.Importers;

public partial class WorkoutsImporter
{
    private static readonly TimeZoneInfo LegacyLocalZone = ResolveLocalZone();

    /// <summary>Gap-fills workouts from the legacy Fitbit exercise-*.json export, skipping any log that already overlaps a modern (UserExercises) workout.</summary>
    private static void ImportLegacyExercises(string folder, Dictionary<long, Workout> workouts)
    {
        var files = Directory.EnumerateFiles(folder, "exercise-*.json");
        var modern = workouts.Values.ToList();

        foreach (var file in files)
        {
            using var stream = File.OpenRead(file);
            using var doc = JsonDocument.Parse(stream);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var entry in doc.RootElement.EnumerateArray())
            {
                if (!entry.TryGetProperty("logId", out var idProp) || !entry.TryGetProperty("startTime", out var startProp))
                {
                    continue;
                }

                var id = idProp.GetInt64();
                if (workouts.ContainsKey(id))
                {
                    continue;
                }

                var start = ParseLegacyLocalTime(startProp.GetString());
                if (start is null)
                {
                    continue;
                }

                var durationMs = entry.TryGetProperty("duration", out var durProp) ? durProp.GetDouble() : 0;
                var end = start.Value.AddMilliseconds(durationMs);

                if (modern.Any(w => w.StartUtc < end && start.Value < w.EndUtc))
                {
                    continue;
                }

                var distanceKm = entry.TryGetProperty("distance", out var distProp) && distProp.ValueKind == JsonValueKind.Number ? distProp.GetDouble() : (double?)null;
                var avgHr = entry.TryGetProperty("averageHeartRate", out var hrProp) && hrProp.ValueKind == JsonValueKind.Number ? hrProp.GetDouble() : (double?)null;
                var speedKmh = entry.TryGetProperty("speed", out var speedProp) && speedProp.ValueKind == JsonValueKind.Number ? speedProp.GetDouble() : (double?)null;
                var elevationGain = entry.TryGetProperty("elevationGain", out var elevProp) && elevProp.ValueKind == JsonValueKind.Number ? elevProp.GetDouble() : (double?)null;
                var hasGps = entry.TryGetProperty("hasGps", out var gpsProp) && gpsProp.ValueKind == JsonValueKind.True;

                workouts[id] = new Workout
                {
                    Id = id,
                    StartUtc = start.Value,
                    EndUtc = end,
                    ActivityName = entry.TryGetProperty("activityName", out var nameProp) ? nameProp.GetString() ?? "Workout" : "Workout",
                    LogType = entry.TryGetProperty("logType", out var logTypeProp) ? logTypeProp.GetString() ?? "" : "",
                    Source = "Legacy Export",
                    DistanceMeters = distanceKm is > 0 ? distanceKm * 1000 : null,
                    Calories = entry.TryGetProperty("calories", out var calProp) && calProp.ValueKind == JsonValueKind.Number ? calProp.GetDouble() : null,
                    Steps = entry.TryGetProperty("steps", out var stepsProp) && stepsProp.ValueKind == JsonValueKind.Number ? stepsProp.GetInt32() : null,
                    AvgHeartRate = NullIfZero(avgHr),
                    AvgSpeedKmh = speedKmh,
                    AvgPaceSecPerKm = speedKmh is > 0 ? 3600.0 / speedKmh : null,
                    ElevationGainMeters = NullIfZero(elevationGain),
                    HasGps = hasGps,
                    IsLegacy = true,
                };
            }
        }
    }

    private static DateTime? ParseLegacyLocalTime(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (!DateTime.TryParseExact(raw, "MM/dd/yy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return null;
        }

        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), LegacyLocalZone);
    }

    private static TimeZoneInfo ResolveLocalZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
        }
    }
}
