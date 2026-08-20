using HealthLens.Api.Services.Csv;
using HealthLens.Api.Services.Import;
using Microsoft.Data.Sqlite;

namespace HealthLens.Api.Services.Importers;

/// <summary>Stress score, daily readiness, SpO2 and nightly skin temperature — all small, one-row-per-day CSVs.</summary>
public sealed class RecoveryImporter : IDomainImporter
{
    public string Name => "Stress & Erholung";

    public Task ImportAsync(ImportContext context, CancellationToken ct)
    {
        var stressFolder = context.FindFolder("Stress Score");
        var pa = context.FindFolder("Physical Activity_GoogleData");

        using var connection = context.OpenWriteConnection();

        if (stressFolder is not null)
        {
            context.ReportProgress("Stress-Score wird gelesen...", 10);
            ImportStressScore(context, connection, Path.Combine(stressFolder, "Stress Score.csv"));
        }

        if (pa is not null)
        {
            context.ReportProgress("Readiness wird gelesen...", 35);
            ImportReadiness(context, connection, Path.Combine(pa, "daily_readiness.csv"));

            context.ReportProgress("Sauerstoffsättigung wird gelesen...", 60);
            ImportSpO2(context, connection, Path.Combine(pa, "daily_oxygen_saturation.csv"));

            context.ReportProgress("Hauttemperatur wird gelesen...", 80);
            ImportTemperature(context, connection, Path.Combine(pa, "daily_sleep_temperature_derivations.csv"));
        }

        context.ReportProgress("Stress & Erholung importiert", 100);
        return Task.CompletedTask;
    }

    private static void ImportStressScore(ImportContext context, SqliteConnection connection, string path)
    {
        using var csv = CsvCursor.Open(path);
        if (csv is null)
        {
            return;
        }

        var date = csv.Column("DATE");
        var score = csv.Column("STRESS_SCORE");
        var status = csv.Column("STATUS");
        var failed = csv.Column("CALCULATION_FAILED");
        if (date < 0 || score < 0)
        {
            return;
        }

        using var writer = new SqliteBulkWriter(
            connection,
            "INSERT OR REPLACE INTO StressScoreDailies (Date, Score, Status) VALUES ($date, $score, $status)",
            "$date", "$score", "$status");

        while (csv.NextRow())
        {
            if (csv.GetBool(failed) is not false || csv.GetUtcDate(date) is not { } day || !csv.TryGetDouble(score, out var value))
            {
                continue;
            }

            writer.Write(day, value, csv.GetString(status));
        }

        Finish(context, writer);
    }

    private static void ImportReadiness(ImportContext context, SqliteConnection connection, string path)
    {
        using var csv = CsvCursor.Open(path);
        if (csv is null)
        {
            return;
        }

        var timestamp = csv.Column("timestamp");
        var score = csv.Column("score");
        if (timestamp < 0 || score < 0)
        {
            return;
        }

        var level = csv.Column("type");
        var sleep = csv.Column("sleep readiness");
        var hrv = csv.Column("heart rate variability readiness");
        var restingHr = csv.Column("resting heart rate readiness");

        using var writer = new SqliteBulkWriter(
            connection,
            """
            INSERT OR REPLACE INTO DailyReadinesses (Date, Score, Level, SleepReadiness, HrvReadiness, RestingHrReadiness)
            VALUES ($date, $score, $level, $sleep, $hrv, $restingHr)
            """,
            "$date", "$score", "$level", "$sleep", "$hrv", "$restingHr");

        while (csv.NextRow())
        {
            if (csv.GetUtcDate(timestamp) is { } day && csv.TryGetDouble(score, out var value))
            {
                writer.Write(day, value, csv.GetString(level), csv.GetString(sleep), csv.GetString(hrv), csv.GetString(restingHr));
            }
        }

        Finish(context, writer);
    }

    private static void ImportSpO2(ImportContext context, SqliteConnection connection, string path)
    {
        using var csv = CsvCursor.Open(path);
        if (csv is null)
        {
            return;
        }

        var timestamp = csv.Column("timestamp");
        var average = csv.Column("average percentage");
        if (timestamp < 0 || average < 0)
        {
            return;
        }

        var lower = csv.Column("lower bound percentage");
        var upper = csv.Column("upper bound percentage");

        using var writer = new SqliteBulkWriter(
            connection,
            "INSERT OR REPLACE INTO SpO2Dailies (Date, AveragePercent, LowerBoundPercent, UpperBoundPercent) VALUES ($date, $avg, $lower, $upper)",
            "$date", "$avg", "$lower", "$upper");

        while (csv.NextRow())
        {
            if (csv.GetUtcDate(timestamp) is { } day && csv.TryGetDouble(average, out var value) && value > 0)
            {
                writer.Write(day, value, csv.GetDoubleOrZero(lower), csv.GetDoubleOrZero(upper));
            }
        }

        Finish(context, writer);
    }

    private static void ImportTemperature(ImportContext context, SqliteConnection connection, string path)
    {
        using var csv = CsvCursor.Open(path);
        if (csv is null)
        {
            return;
        }

        var timestamp = csv.Column("timestamp");
        var nightly = csv.Column("nightly temperature celsius");
        if (timestamp < 0 || nightly < 0)
        {
            return;
        }

        var baselineColumn = csv.Column("baseline temperature celsius");

        using var writer = new SqliteBulkWriter(
            connection,
            "INSERT OR REPLACE INTO TemperatureNightlies (Date, NightlyCelsius, BaselineCelsius) VALUES ($date, $nightly, $baseline)",
            "$date", "$nightly", "$baseline");

        while (csv.NextRow())
        {
            if (csv.GetUtcDate(timestamp) is not { } day || !csv.TryGetDouble(nightly, out var value))
            {
                continue;
            }

            var baseline = csv.GetDouble(baselineColumn);
            writer.Write(day, value, double.IsNaN(baseline ?? 0) ? null : baseline);
        }

        Finish(context, writer);
    }

    private static void Finish(ImportContext context, SqliteBulkWriter writer)
    {
        writer.Flush();
        context.AddRows(writer.RowCount);
    }
}
