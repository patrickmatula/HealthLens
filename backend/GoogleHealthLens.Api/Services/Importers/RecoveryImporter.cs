using System.Globalization;
using CsvHelper;
using GoogleHealthLens.Api.Data;
using GoogleHealthLens.Api.Models;

namespace GoogleHealthLens.Api.Services.Importers;

/// <summary>Stress score, daily readiness, SpO2 and nightly skin temperature — all small, one-row-per-day CSVs.</summary>
public class RecoveryImporter : IDomainImporter
{
    public string Name => "Stress & Erholung";

    public async Task ImportAsync(ImportContext context, CancellationToken ct)
    {
        var stressFolder = context.FindFolder("Stress Score");
        var pa = context.FindFolder("Physical Activity_GoogleData");

        await using var db = context.CreateContext();
        long count = 0;

        if (stressFolder is not null)
        {
            context.ReportProgress("Stress-Score wird gelesen...", 10);
            count += await ImportStressScore(db, Path.Combine(stressFolder, "Stress Score.csv"), ct);
        }

        if (pa is not null)
        {
            context.ReportProgress("Readiness wird gelesen...", 35);
            count += await ImportReadiness(db, Path.Combine(pa, "daily_readiness.csv"), ct);

            context.ReportProgress("Sauerstoffsättigung wird gelesen...", 60);
            count += await ImportSpO2(db, Path.Combine(pa, "daily_oxygen_saturation.csv"), ct);

            context.ReportProgress("Hauttemperatur wird gelesen...", 80);
            count += await ImportTemperature(db, Path.Combine(pa, "daily_sleep_temperature_derivations.csv"), ct);
        }

        await db.SaveChangesAsync(ct);
        context.RowsImported += count;
        context.ReportProgress("Stress & Erholung importiert", 100);
    }

    private static async Task<long> ImportStressScore(AppDbContext db, string path, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return 0;
        }

        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        csv.Read();
        csv.ReadHeader();
        long count = 0;
        while (csv.Read())
        {
            if (!bool.TryParse(csv.GetField("CALCULATION_FAILED"), out var failed) || failed)
            {
                continue;
            }

            var date = ParseDateOnly(csv.GetField("DATE"));
            if (date is null || !double.TryParse(csv.GetField("STRESS_SCORE"), NumberStyles.Float, CultureInfo.InvariantCulture, out var score))
            {
                continue;
            }

            var existing = await db.StressScoreDailies.FindAsync([date.Value], ct);
            if (existing is null)
            {
                db.StressScoreDailies.Add(new StressScoreDaily { Date = date.Value, Score = score, Status = csv.GetField("STATUS") ?? "" });
                count++;
            }
        }

        return count;
    }

    private static async Task<long> ImportReadiness(AppDbContext db, string path, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return 0;
        }

        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        csv.Read();
        csv.ReadHeader();
        long count = 0;
        while (csv.Read())
        {
            var date = ParseDateOnly(csv.GetField("timestamp"));
            if (date is null || !double.TryParse(csv.GetField("score"), NumberStyles.Float, CultureInfo.InvariantCulture, out var score))
            {
                continue;
            }

            var existing = await db.DailyReadinesses.FindAsync([date.Value], ct);
            if (existing is null)
            {
                db.DailyReadinesses.Add(new DailyReadiness
                {
                    Date = date.Value,
                    Score = score,
                    Level = csv.GetField("type") ?? "",
                    SleepReadiness = csv.GetField("sleep readiness") ?? "",
                    HrvReadiness = csv.GetField("heart rate variability readiness") ?? "",
                    RestingHrReadiness = csv.GetField("resting heart rate readiness") ?? "",
                });
                count++;
            }
        }

        return count;
    }

    private static async Task<long> ImportSpO2(AppDbContext db, string path, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return 0;
        }

        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        csv.Read();
        csv.ReadHeader();
        long count = 0;
        while (csv.Read())
        {
            var date = ParseDateOnly(csv.GetField("timestamp"));
            if (date is null || !double.TryParse(csv.GetField("average percentage"), NumberStyles.Float, CultureInfo.InvariantCulture, out var avg) || avg <= 0)
            {
                continue;
            }

            var existing = await db.SpO2Dailies.FindAsync([date.Value], ct);
            if (existing is null)
            {
                db.SpO2Dailies.Add(new SpO2Daily
                {
                    Date = date.Value,
                    AveragePercent = avg,
                    LowerBoundPercent = ParseDoubleOrZero(csv.GetField("lower bound percentage")),
                    UpperBoundPercent = ParseDoubleOrZero(csv.GetField("upper bound percentage")),
                });
                count++;
            }
        }

        return count;
    }

    private static async Task<long> ImportTemperature(AppDbContext db, string path, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return 0;
        }

        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        csv.Read();
        csv.ReadHeader();
        long count = 0;
        while (csv.Read())
        {
            var date = ParseDateOnly(csv.GetField("timestamp"));
            if (date is null || !double.TryParse(csv.GetField("nightly temperature celsius"), NumberStyles.Float, CultureInfo.InvariantCulture, out var nightly))
            {
                continue;
            }

            var baselineRaw = csv.GetField("baseline temperature celsius");
            double? baseline = baselineRaw is not null && double.TryParse(baselineRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var b) && !double.IsNaN(b) ? b : null;

            var existing = await db.TemperatureNightlies.FindAsync([date.Value], ct);
            if (existing is null)
            {
                db.TemperatureNightlies.Add(new TemperatureNightly { Date = date.Value, NightlyCelsius = nightly, BaselineCelsius = baseline });
                count++;
            }
        }

        return count;
    }

    private static double ParseDoubleOrZero(string? s) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static DateOnly? ParseDateOnly(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt)
            ? DateOnly.FromDateTime(dt)
            : null;
    }
}
