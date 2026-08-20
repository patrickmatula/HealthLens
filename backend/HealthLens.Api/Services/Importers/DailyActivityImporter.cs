using System.Runtime.InteropServices;
using HealthLens.Api.Models;
using HealthLens.Api.Services.Csv;
using HealthLens.Api.Services.Import;
using Microsoft.EntityFrameworkCore;

namespace HealthLens.Api.Services.Importers;

/// <summary>
/// Aggregates the per-event Physical Activity_GoogleData time-series CSVs (steps_, distance_, calories_,
/// active_minutes_, sedentary_period_) into one row per local calendar date. Each file is independent,
/// so all of them — across all five metrics — are parsed in parallel and their day totals merged after.
/// </summary>
public sealed class DailyActivityImporter : IDomainImporter
{
    private static readonly (string Prefix, Metric Metric)[] Sources =
    [
        ("steps_", Metric.Steps),
        ("distance_", Metric.Distance),
        ("calories_", Metric.Calories),
        ("active_minutes_", Metric.ActiveMinutes),
        ("sedentary_period_", Metric.Sedentary),
    ];

    private enum Metric
    {
        Steps,
        Distance,
        Calories,
        ActiveMinutes,
        Sedentary,
    }

    public string Name => "Tagesaktivität";

    public async Task ImportAsync(ImportContext context, CancellationToken ct)
    {
        var folder = context.FindFolder("Physical Activity_GoogleData");
        if (folder is null)
        {
            return;
        }

        var work = Sources
            .SelectMany(source => EnumerateDataFiles(folder, source.Prefix), (source, file) => (File: file, source.Metric))
            .ToArray();

        var days = new Dictionary<DateOnly, DayTotals>();
        context.ReportProgress($"Tagesaktivität wird gelesen ({work.Length} Dateien)...", 0);

        await ParallelCsv.RunAsync(
            work,
            item => ParseFile(item.File, item.Metric),
            partial => Merge(days, partial),
            done => context.ReportProgress($"Tagesaktivität wird gelesen ({done}/{work.Length})...", 85 * done / work.Length),
            ct);

        context.ReportProgress("Tagesaktivität wird gespeichert...", 90);
        await using var db = context.CreateContext();
        var existing = await db.DailyActivitySummaries.ToDictionaryAsync(d => d.Date, ct);

        foreach (var (date, totals) in days)
        {
            if (!existing.TryGetValue(date, out var row))
            {
                row = new DailyActivitySummary { Date = date };
                db.DailyActivitySummaries.Add(row);
            }

            // A zero total means this export carried no data for the metric that day, not a real zero —
            // keep whatever an earlier import already established.
            row.Steps = totals.Steps > 0 ? (int)totals.Steps : row.Steps;
            row.DistanceMeters = totals.DistanceMeters > 0 ? totals.DistanceMeters : row.DistanceMeters;
            row.CaloriesTotal = totals.Calories > 0 ? totals.Calories : row.CaloriesTotal;
            row.ActiveMinutes = totals.ActiveMinutes > 0 ? totals.ActiveMinutes : row.ActiveMinutes;
            row.SedentaryMinutes = totals.SedentaryMinutes > 0 ? totals.SedentaryMinutes : row.SedentaryMinutes;
        }

        await db.SaveChangesAsync(ct);
        context.AddRows(days.Count);
        context.ReportProgress("Tagesaktivität importiert", 100);
    }

    private static Dictionary<DateOnly, DayTotals> ParseFile(string file, Metric metric)
    {
        var days = new Dictionary<DateOnly, DayTotals>();
        using var csv = CsvCursor.Open(file);
        if (csv is null)
        {
            return days;
        }

        if (metric == Metric.Sedentary)
        {
            var startColumn = csv.Column("start time");
            var endColumn = csv.Column("end time");
            while (startColumn >= 0 && endColumn >= 0 && csv.NextRow())
            {
                if (csv.GetUtc(startColumn) is not { } start || csv.GetUtc(endColumn) is not { } end)
                {
                    continue;
                }

                Day(days, start).SedentaryMinutes += (int)Math.Round((end - start).TotalMinutes);
            }

            return days;
        }

        var timestampColumn = csv.Column("timestamp");
        if (timestampColumn < 0)
        {
            return days;
        }

        if (metric == Metric.ActiveMinutes)
        {
            var light = csv.Column("light");
            var moderate = csv.Column("moderate");
            var very = csv.Column("very");
            while (csv.NextRow())
            {
                if (csv.GetUtc(timestampColumn) is not { } timestamp)
                {
                    continue;
                }

                Day(days, timestamp).ActiveMinutes += csv.GetInt32OrZero(light) + csv.GetInt32OrZero(moderate) + csv.GetInt32OrZero(very);
            }

            return days;
        }

        // These files name their value column after the metric ("steps", "distance", ...).
        var valueColumn = csv.ValueColumn("timestamp", "data source");
        while (valueColumn >= 0 && csv.NextRow())
        {
            if (csv.GetUtc(timestampColumn) is not { } timestamp || !csv.TryGetDouble(valueColumn, out var value))
            {
                continue;
            }

            ref var totals = ref Day(days, timestamp);
            switch (metric)
            {
                case Metric.Steps:
                    totals.Steps += (long)value;
                    break;
                case Metric.Distance:
                    totals.DistanceMeters += value;
                    break;
                default:
                    totals.Calories += value;
                    break;
            }
        }

        return days;
    }

    private static ref DayTotals Day(Dictionary<DateOnly, DayTotals> days, DateTime utc) =>
        ref CollectionsMarshal.GetValueRefOrAddDefault(days, LocalTimeZone.ToLocalDate(utc), out _);

    private static void Merge(Dictionary<DateOnly, DayTotals> days, Dictionary<DateOnly, DayTotals> partial)
    {
        foreach (var (date, totals) in partial)
        {
            ref var target = ref CollectionsMarshal.GetValueRefOrAddDefault(days, date, out _);
            target.Steps += totals.Steps;
            target.DistanceMeters += totals.DistanceMeters;
            target.Calories += totals.Calories;
            target.ActiveMinutes += totals.ActiveMinutes;
            target.SedentaryMinutes += totals.SedentaryMinutes;
        }
    }

    private static IEnumerable<string> EnumerateDataFiles(string folder, string prefix) =>
        Directory.EnumerateFiles(folder, $"{prefix}*.csv").Where(f => !Path.GetFileName(f).Contains("readme", StringComparison.OrdinalIgnoreCase));

    private struct DayTotals
    {
        public long Steps;
        public double DistanceMeters;
        public double Calories;
        public int ActiveMinutes;
        public int SedentaryMinutes;
    }
}
