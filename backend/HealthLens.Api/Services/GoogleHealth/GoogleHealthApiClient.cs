using System.Net.Http.Headers;
using System.Text.Json;

namespace HealthLens.Api.Services.GoogleHealth;

public sealed record GoogleHealthDataPoint(DateTimeOffset StartUtc, DateTimeOffset EndUtc, JsonElement Raw);

/// <summary>
/// Thin wrapper around the Google Health API's users.dataTypes.dataPoints.list endpoint. The exact
/// per-data-type field names inside each DataPoint are not fully documented publicly, so callers get
/// the raw JsonElement back and pick values out defensively (see GoogleHealthFieldReader) instead of
/// this client asserting a schema it can't be certain of.
/// </summary>
public sealed class GoogleHealthApiClient(HttpClient http, ILogger<GoogleHealthApiClient> logger)
{
    public async Task<IReadOnlyList<GoogleHealthDataPoint>> ListDataPointsAsync(
        string accessToken, string dataTypeId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct)
    {
        var results = new List<GoogleHealthDataPoint>();
        string? pageToken = null;
        var filter = $"startTime>=\"{fromUtc:O}\" AND startTime<=\"{toUtc:O}\"";

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
                    if (TryReadInterval(point, out var start, out var end))
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

    private static bool TryReadInterval(JsonElement point, out DateTimeOffset start, out DateTimeOffset end)
    {
        start = default;
        end = default;

        // The interval has shown up either at the top level or nested under "interval" in different
        // Google API generations — try both rather than betting on one.
        var container = point.TryGetProperty("interval", out var interval) ? interval : point;

        if (!container.TryGetProperty("startTime", out var startProp) || !DateTimeOffset.TryParse(startProp.GetString(), out start))
        {
            return false;
        }

        if (container.TryGetProperty("endTime", out var endProp) && DateTimeOffset.TryParse(endProp.GetString(), out var parsedEnd))
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
/// Picks a numeric value out of a DataPoint's data union by trying every plausible field name for that
/// data type, since the exact field names aren't publicly documented in detail. Logs the keys actually
/// present the first time none of the candidates match, so a follow-up fix is a one-line change instead
/// of another guessing round.
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
            if (union.TryGetProperty(field, out var value))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var d))
                {
                    return d;
                }

                if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("value", out var nested) && nested.TryGetDouble(out var nd))
                {
                    return nd;
                }
            }
        }

        var seenKeys = string.Join(", ", union.EnumerateObject().Select(p => p.Name));
        logger.LogWarning(
            "Google Health data point '{Field}' had none of the expected sub-fields ({Candidates}); actual keys: {Keys}",
            unionFieldName, string.Join('/', candidateFields), seenKeys);
        return null;
    }
}
