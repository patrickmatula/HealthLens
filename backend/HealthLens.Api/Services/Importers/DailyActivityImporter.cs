using System.Globalization;
using CsvHelper;
using HealthLens.Api.Models;

namespace HealthLens.Api.Services.Importers;

/// <summary>
/// Aggregates the per-event Physical Activity_GoogleData time-series CSVs (steps_, distance_, calories_,
/// active_minutes_, sedentary_period_) into one row per local calendar date.
/// </summary>
public class DailyActivityImporter : IDomainImporter
{
    // Fitbit/Pixel Watch data for this account is logged in Central European time; bucket "daily" totals by that,
    // matching what the Fitbit app itself would show as "today".
    private static readonly TimeZoneInfo LocalZone = ResolveLocalZone();

    public string Name => "Tagesaktivität";

    public async Task ImportAsync(ImportContext context, CancellationToken ct)
    {
        var folder = context.FindFolder("Physical Activity_GoogleData");
        if (folder is null)
        {
            return;
        }

        var days = new Dictionary<DateOnly, DayAccumulator>();

        context.ReportProgress("Schritte werden gelesen...", 0);
        SumInto(folder, "steps_", days, (acc, value) => acc.Steps += (int)value);

        context.ReportProgress("Distanz wird gelesen...", 20);
        SumInto(folder, "distance_", days, (acc, value) => acc.DistanceMeters += value);

        context.ReportProgress("Kalorien werden gelesen...", 40);
        SumInto(folder, "calories_", days, (acc, value) => acc.CaloriesTotal += value);

        context.ReportProgress("Aktive Minuten werden gelesen...", 60);
        ImportActiveMinutes(folder, days);

        context.ReportProgress("Sitzzeit wird gelesen...", 80);
        ImportSedentary(folder, days);

        context.ReportProgress("Tagesaktivität wird gespeichert...", 90);
        await using var db = context.CreateContext();
        foreach (var (date, acc) in days)
        {
            var existing = await db.DailyActivitySummaries.FindAsync([date], ct);
            if (existing is null)
            {
                existing = new DailyActivitySummary { Date = date };
                db.DailyActivitySummaries.Add(existing);
            }

            existing.Steps = acc.Steps > 0 ? acc.Steps : existing.Steps;
            existing.DistanceMeters = acc.DistanceMeters > 0 ? acc.DistanceMeters : existing.DistanceMeters;
            existing.CaloriesTotal = acc.CaloriesTotal > 0 ? acc.CaloriesTotal : existing.CaloriesTotal;
            existing.ActiveMinutes = acc.ActiveMinutes > 0 ? acc.ActiveMinutes : existing.ActiveMinutes;
            existing.SedentaryMinutes = acc.SedentaryMinutes > 0 ? acc.SedentaryMinutes : existing.SedentaryMinutes;
        }

        await db.SaveChangesAsync(ct);
        context.RowsImported += days.Count;
        context.ReportProgress("Tagesaktivität importiert", 100);
    }

    private void SumInto(string folder, string prefix, Dictionary<DateOnly, DayAccumulator> days, Action<DayAccumulator, double> add)
    {
        foreach (var file in EnumerateDataFiles(folder, prefix))
        {
            using var reader = new StreamReader(file);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            csv.Read();
            csv.ReadHeader();
            var valueField = csv.HeaderRecord!.First(h => h != "timestamp" && h != "data source");

            while (csv.Read())
            {
                var ts = ParseUtc(csv.GetField("timestamp"));
                if (ts is null || !double.TryParse(csv.GetField(valueField), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    continue;
                }

                var date = ToLocalDate(ts.Value);
                add(GetOrAdd(days, date), value);
            }
        }
    }

    private void ImportActiveMinutes(string folder, Dictionary<DateOnly, DayAccumulator> days)
    {
        foreach (var file in EnumerateDataFiles(folder, "active_minutes_"))
        {
            using var reader = new StreamReader(file);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            csv.Read();
            csv.ReadHeader();

            while (csv.Read())
            {
                var ts = ParseUtc(csv.GetField("timestamp"));
                if (ts is null)
                {
                    continue;
                }

                var light = ParseIntOrZero(csv.GetField("light"));
                var moderate = ParseIntOrZero(csv.GetField("moderate"));
                var very = ParseIntOrZero(csv.GetField("very"));
                GetOrAdd(days, ToLocalDate(ts.Value)).ActiveMinutes += light + moderate + very;
            }
        }
    }

    private void ImportSedentary(string folder, Dictionary<DateOnly, DayAccumulator> days)
    {
        foreach (var file in EnumerateDataFiles(folder, "sedentary_period_"))
        {
            using var reader = new StreamReader(file);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            csv.Read();
            csv.ReadHeader();

            while (csv.Read())
            {
                var start = ParseUtc(csv.GetField("start time"));
                var end = ParseUtc(csv.GetField("end time"));
                if (start is null || end is null)
                {
                    continue;
                }

                var minutes = (end.Value - start.Value).TotalMinutes;
                GetOrAdd(days, ToLocalDate(start.Value)).SedentaryMinutes += (int)Math.Round(minutes);
            }
        }
    }

    private static IEnumerable<string> EnumerateDataFiles(string folder, string prefix) =>
        Directory.EnumerateFiles(folder, $"{prefix}*.csv").Where(f => !Path.GetFileName(f).Contains("readme", StringComparison.OrdinalIgnoreCase));

    private static int ParseIntOrZero(string? s) => int.TryParse(s, out var v) ? v : 0;

    private static DateTime? ParseUtc(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt)
            ? dt
            : null;
    }

    private static DateOnly ToLocalDate(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, LocalZone));

    private static DayAccumulator GetOrAdd(Dictionary<DateOnly, DayAccumulator> days, DateOnly date)
    {
        if (!days.TryGetValue(date, out var acc))
        {
            acc = new DayAccumulator();
            days[date] = acc;
        }

        return acc;
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

    private class DayAccumulator
    {
        public int Steps;
        public double DistanceMeters;
        public double CaloriesTotal;
        public int ActiveMinutes;
        public int SedentaryMinutes;
    }
}
