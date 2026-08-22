using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace HealthLens.Api.Services.GoogleHealth;

public sealed record GoogleHealthDataPoint(DateTimeOffset StartUtc, DateTimeOffset EndUtc, JsonElement Raw);

/// <summary>
/// Thin wrapper around the Google Health API's users.dataTypes.dataPoints.list endpoint. Per the official
/// REST reference (developers.google.com/health/reference/rest/v4/users.dataTypes.dataPoints), every field
/// — the time interval/sample time and the value itself — lives nested under the data type's own union
/// object (e.g. a "steps" data point is shaped { "steps": { "interval": { "startTime": ... }, "count": "1250" } }),
/// not at the top level. int64-typed values (step counts, millimeter distances) are JSON-encoded as strings
/// to avoid precision loss, not as JSON numbers.
/// </summary>
public sealed class GoogleHealthApiClient(HttpClient http, ILogger<GoogleHealthApiClient> logger)
{
    public async Task<IReadOnlyList<GoogleHealthDataPoint>> ListDataPointsAsync(
        string accessToken, string dataTypeId, string unionFieldName, bool isSampleType,
        DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct)
    {
        var results = new List<GoogleHealthDataPoint>();
        string? pageToken = null;

        // Filter fields must be qualified with the data type's filter identifier (underscore form of the
        // URL's hyphenated dataTypeId, e.g. "active-energy-burned" -> "active_energy_burned") followed by
        // the field path for that type's time kind -- interval types (steps, distance, ...) expose
        // "interval.start_time", sample types (weight, body-fat, ...) expose "sample_time.physical_time".
        // A bare "startTime" is rejected outright with a 400 INVALID_DATA_POINT_FILTER_RESTRICTION_COMPARABLE.
        // The API also only accepts GREATER_THAN_EQUALS/LESS_THAN on these fields -- "<=" on the upper
        // bound is rejected too (INVALID_DATA_POINT_FILTER_RESTRICTION_COMPARATOR).
        var filterField = dataTypeId.Replace('-', '_') + (isSampleType ? ".sample_time.physical_time" : ".interval.start_time");
        var filter = $"{filterField}>=\"{fromUtc:O}\" AND {filterField}<\"{toUtc:O}\"";

        do
        {
            var url = $"v4/users/me/dataTypes/{dataTypeId}/dataPoints?pageSize=1000&filter={Uri.EscapeDataString(filter)}"
                + (pageToken is null ? "" : $"&pageToken={Uri.EscapeDataString(pageToken)}");

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Google Health API list({DataType}) failed ({Status}): {Body}", dataTypeId, response.StatusCode, body);
                return results;
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("dataPoints", out var points))
            {
                foreach (var point in points.EnumerateArray())
                {
                    if (TryReadInterval(point, unionFieldName, isSampleType, out var start, out var end))
                    {
                        results.Add(new GoogleHealthDataPoint(start, end, point.Clone()));
                    }
                }
            }

            pageToken = doc.RootElement.TryGetProperty("nextPageToken", out var next) && next.ValueKind == JsonValueKind.String
                ? next.GetString()
                : null;
        } while (!string.IsNullOrEmpty(pageToken));

        return results;
    }

    private static bool TryReadInterval(JsonElement point, string unionFieldName, bool isSampleType, out DateTimeOffset start, out DateTimeOffset end)
    {
        start = default;
        end = default;

        if (!point.TryGetProperty(unionFieldName, out var union))
        {
            return false;
        }

        if (isSampleType)
        {
            // Sample types (weight, body-fat) are a single instant: union.sampleTime.physicalTime.
            if (!union.TryGetProperty("sampleTime", out var sampleTime)
                || !sampleTime.TryGetProperty("physicalTime", out var physicalTimeProp)
                || !DateTimeOffset.TryParse(physicalTimeProp.GetString(), out var physicalTime))
            {
                return false;
            }

            start = end = physicalTime;
            return true;
        }

        // Interval types (steps, distance, active-energy-burned) are a range: union.interval.{startTime,endTime}.
        if (!union.TryGetProperty("interval", out var interval)
            || !interval.TryGetProperty("startTime", out var startProp)
            || !DateTimeOffset.TryParse(startProp.GetString(), out start))
        {
            return false;
        }

        if (interval.TryGetProperty("endTime", out var endProp) && DateTimeOffset.TryParse(endProp.GetString(), out var parsedEnd))
        {
            end = parsedEnd;
        }
        else
        {
            end = start;
        }

        return true;
    }
}

/// <summary>
/// Picks a numeric value out of a DataPoint's data union by field name. int64-typed fields (step counts,
/// millimeter distances) arrive JSON-encoded as strings rather than numbers, so both kinds are accepted.
/// </summary>
public static class GoogleHealthFieldReader
{
    public static double? ReadNumeric(JsonElement dataPoint, string unionFieldName, ILogger logger, params string[] candidateFields)
    {
        if (!dataPoint.TryGetProperty(unionFieldName, out var union))
        {
            return null;
        }

        foreach (var field in candidateFields)
        {
            if (!union.TryGetProperty(field, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var d))
            {
                return d;
            }

            if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var sd))
            {
                return sd;
            }
        }

        var seenKeys = string.Join(", ", union.EnumerateObject().Select(p => p.Name));
        logger.LogWarning(
            "Google Health data point '{Field}' had none of the expected sub-fields ({Candidates}); actual keys: {Keys}",
            unionFieldName, string.Join('/', candidateFields), seenKeys);
        return null;
    }
}
