using System.Text.RegularExpressions;

namespace HealthLens.Api.Services;

/// <summary>
/// C# port of frontend/src/utils/format.ts's categorizeWorkout. Needs to exist server-side too because
/// default-shoe assignment (ShoeDefaultsStore) happens at import/sync time, before anything reaches the
/// frontend — keep both in sync by hand if the classification rules ever change.
/// </summary>
public static partial class WorkoutCategorizer
{
    public static readonly string[] Categories = ["Lauf", "Spaziergang", "Rad", "Kraft", "Sonstiges"];

    public static string Categorize(string activityName, double? avgPaceSecPerKm, double? cadenceAvgSpm)
    {
        var name = activityName.ToLowerInvariant();
        if (RunKeywords().IsMatch(name)) return "Lauf";
        if (WalkKeywords().IsMatch(name)) return "Spaziergang";
        if (BikeKeywords().IsMatch(name)) return "Rad";
        if (StrengthKeywords().IsMatch(name)) return "Kraft";
        if (cadenceAvgSpm is { } cadence) return cadence >= 140 ? "Lauf" : "Spaziergang";
        if (avgPaceSecPerKm is { } pace) return pace < 480 ? "Lauf" : "Spaziergang";
        return "Sonstiges";
    }

    [GeneratedRegex("(run|laufen|hike|wander)")]
    private static partial Regex RunKeywords();

    [GeneratedRegex("(walk|geh)")]
    private static partial Regex WalkKeywords();

    [GeneratedRegex("(bike|cycl|rad)")]
    private static partial Regex BikeKeywords();

    [GeneratedRegex("(strength|hiit|aerobic|kraft|gym)")]
    private static partial Regex StrengthKeywords();
}
