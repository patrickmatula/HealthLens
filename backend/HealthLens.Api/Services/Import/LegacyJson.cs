using System.Text.Json;

namespace HealthLens.Api.Services.Import;

/// <summary>
/// Defensive property readers for the legacy Fitbit *.json export: a property can be absent, null, or
/// (in older export vintages) a different type than expected, and none of that should throw and abort
/// parsing the rest of the file.
/// </summary>
public static class LegacyJson
{
    public static string? String(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    public static double? Number(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.TryGetDouble(out var number) ? number : null;

    public static bool Bool(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;
}
