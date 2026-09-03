using System.Collections.Concurrent;
using System.Text.Json;

namespace HealthLens.Api.Services;

public record WorkoutWeather(double TemperatureCelsius, double? HumidityPercent);

/// <summary>
/// Optional, on-demand historical weather lookup for a workout's GPS location and start time, via
/// Open-Meteo's free Archive API (open-meteo.com/en/docs/historical-weather-api) — no API key, no
/// account, no rate limit that a single self-hosted user could realistically hit. This is the one place
/// HealthLens sends anything to a third party beyond the user's own explicitly-configured Google Health
/// connection: only a workout's GPS coordinate and date, only when the frontend's weather toggle is on
/// and only for a workout the user is actually viewing — never a bulk export of location history. See the
/// README Privacy section.
///
/// Cached in memory per workout id for the process lifetime: the underlying data (a day that already
/// happened) never changes, so there's no reason to hit the API again for the same workout, but nothing
/// here needs to survive a restart badly enough to justify a persisted store.
/// </summary>
public sealed class OpenMeteoWeatherService(HttpClient httpClient, ILogger<OpenMeteoWeatherService> logger)
{
    // AddHttpClient<T> registers the typed client as transient, so a plain instance field would reset on
    // every DI resolution -- static keeps the cache alive for the process lifetime regardless.
    private static readonly ConcurrentDictionary<long, WorkoutWeather?> Cache = new();

    public async Task<WorkoutWeather?> GetWeatherAsync(long workoutId, double latitude, double longitude, DateTime startUtc, CancellationToken ct)
    {
        if (Cache.TryGetValue(workoutId, out var cached))
        {
            return cached;
        }

        try
        {
            var date = startUtc.ToString("yyyy-MM-dd");
            var url = $"v1/archive?latitude={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                      $"&longitude={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                      $"&start_date={date}&end_date={date}&hourly=temperature_2m,relative_humidity_2m&timezone=UTC";

            using var response = await httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                Cache[workoutId] = null;
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var hourly = doc.RootElement.GetProperty("hourly");
            var times = hourly.GetProperty("time");
            var temps = hourly.GetProperty("temperature_2m");
            var humidity = hourly.GetProperty("relative_humidity_2m");

            // Pick the hourly sample closest to the workout's actual start time.
            var bestIndex = -1;
            var bestDiff = TimeSpan.MaxValue;
            for (var i = 0; i < times.GetArrayLength(); i++)
            {
                if (!DateTime.TryParse(times[i].GetString(), out var sampleTime))
                {
                    continue;
                }

                var diff = (sampleTime - startUtc).Duration();
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0 || temps[bestIndex].ValueKind != JsonValueKind.Number)
            {
                Cache[workoutId] = null;
                return null;
            }

            var result = new WorkoutWeather(
                temps[bestIndex].GetDouble(),
                humidity[bestIndex].ValueKind == JsonValueKind.Number ? humidity[bestIndex].GetDouble() : null);
            Cache[workoutId] = result;
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Weather lookup failed for workout {WorkoutId}", workoutId);
            Cache[workoutId] = null;
            return null;
        }
    }
}
