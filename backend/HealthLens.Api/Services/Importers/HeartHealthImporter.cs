using System.Runtime.InteropServices;
using HealthLens.Api.Services.Csv;
using HealthLens.Api.Services.Import;
using Microsoft.Data.Sqlite;

namespace HealthLens.Api.Services.Importers;

/// <summary>
/// Resting HR, AZM zone minutes, HRV (daily + 5-min sleep detail), respiratory rate, and a curated
/// 1-minute aggregate of the continuous intraday heart rate stream — by far the largest source in a
/// Takeout export, at hundreds of daily files and millions of rows. Every table here is a plain keyed
/// upsert, so the whole importer runs on batched raw SQL: files are parsed across all cores and the
/// rows funnelled into a single writer, instead of round-tripping through EF change tracking.
/// </summary>
public sealed class HeartHealthImporter : IDomainImporter
{
    public string Name => "Herzfrequenz & HRV";

    public async Task ImportAsync(ImportContext context, CancellationToken ct)
    {
        var folder = context.FindFolder("Physical Activity_GoogleData");
        if (folder is null)
        {
            return;
        }

        await using var connection = context.OpenWriteConnection();

        context.ReportProgress("Ruhepuls wird gelesen...", 0);
        ImportDailyValue(context, connection, Path.Combine(folder, "daily_resting_heart_rate.csv"), "beats per minute",
            "INSERT OR REPLACE INTO RestingHeartRateDailies (Date, Bpm) VALUES ($date, $value)");

        context.ReportProgress("Atemfrequenz wird gelesen...", 5);
        ImportDailyValue(context, connection, Path.Combine(folder, "daily_respiratory_rate.csv"), "breaths per minute",
            "INSERT OR REPLACE INTO RespiratoryRateDailies (Date, BreathsPerMinute) VALUES ($date, $value)");

        context.ReportProgress("Active Zone Minutes werden gelesen...", 10);
        await ImportAzmAsync(context, connection, folder, ct);

        context.ReportProgress("HRV-Tageswerte werden gelesen...", 20);
        ImportHrvDaily(context, connection, folder);

        context.ReportProgress("HRV-Details werden gelesen...", 30);
        await ImportHrvDetailAsync(context, connection, folder, ct);

        context.ReportProgress("Herzfrequenz-Verlauf wird aggregiert (das kann etwas dauern)...", 40);
        await ImportIntradayHeartRateAsync(context, connection, folder, ct);

        context.ReportProgress("Herzfrequenz & HRV importiert", 100);
    }

    private static void ImportDailyValue(ImportContext context, SqliteConnection connection, string path, string valueColumn, string sql)
    {
        using var csv = CsvCursor.Open(path);
        if (csv is null)
        {
            return;
        }

        var timestamp = csv.Column("timestamp");
        var value = csv.Column(valueColumn);
        if (timestamp < 0 || value < 0)
        {
            return;
        }

        using var writer = new SqliteBulkWriter(connection, sql, "$date", "$value");
        while (csv.NextRow())
        {
            if (csv.GetUtcDate(timestamp) is { } date && csv.TryGetDouble(value, out var parsed))
            {
                writer.Write(date, parsed);
            }
        }

        writer.Flush();
        context.AddRows(writer.RowCount);
    }

    private static async Task ImportAzmAsync(ImportContext context, SqliteConnection connection, string folder, CancellationToken ct)
    {
        // Zone minutes arrive as intraday events, so they have to be totalled per local day before
        // anything can be written — merge every file's partial totals first, then write once.
        var totals = new Dictionary<(DateOnly Date, string Zone), int>();

        await ParallelCsv.RunAsync(
            Directory.GetFiles(folder, "active_zone_minutes_*.csv"),
            ParseAzmFile,
            partial =>
            {
                foreach (var (key, minutes) in partial)
                {
                    CollectionsMarshal.GetValueRefOrAddDefault(totals, key, out _) += minutes;
                }
            },
            null,
            ct);

        using var writer = new SqliteBulkWriter(
            connection,
            "INSERT OR REPLACE INTO HeartRateZoneMinutesDailies (Date, Zone, Minutes) VALUES ($date, $zone, $minutes)",
            "$date", "$zone", "$minutes");

        foreach (var ((date, zone), minutes) in totals)
        {
            writer.Write(date, zone, minutes);
        }

        writer.Flush();
        context.AddRows(writer.RowCount);
    }

    private static Dictionary<(DateOnly Date, string Zone), int> ParseAzmFile(string file)
    {
        var totals = new Dictionary<(DateOnly, string), int>();
        using var csv = CsvCursor.Open(file);
        if (csv is null)
        {
            return totals;
        }

        var timestamp = csv.Column("timestamp");
        var zone = csv.Column("heart rate zone");
        var minutes = csv.Column("total minutes");
        while (timestamp >= 0 && zone >= 0 && minutes >= 0 && csv.NextRow())
        {
            if (csv.GetUtc(timestamp) is not { } utc || csv.Field(zone).IsEmpty)
            {
                continue;
            }

            var key = (LocalTimeZone.ToLocalDate(utc), csv.GetString(zone));
            CollectionsMarshal.GetValueRefOrAddDefault(totals, key, out _) += csv.GetInt32OrZero(minutes);
        }

        return totals;
    }

    private static void ImportHrvDaily(ImportContext context, SqliteConnection connection, string folder)
    {
        using var csv = CsvCursor.Open(Path.Combine(folder, "daily_heart_rate_variability.csv"));
        if (csv is null)
        {
            return;
        }

        var timestamp = csv.Column("timestamp");
        if (timestamp < 0)
        {
            return;
        }

        var rmssd = csv.Column("average heart rate variability milliseconds");
        var nonRem = csv.Column("non rem heart rate beats per minute");
        var entropy = csv.Column("entropy");

        using var writer = new SqliteBulkWriter(
            connection,
            "INSERT OR REPLACE INTO HrvDailySummaries (Date, RmssdMs, NonRemHrBpm, Entropy) VALUES ($date, $rmssd, $nonRem, $entropy)",
            "$date", "$rmssd", "$nonRem", "$entropy");

        while (csv.NextRow())
        {
            if (csv.GetUtcDate(timestamp) is { } date)
            {
                writer.Write(date, csv.GetDoubleOrZero(rmssd), csv.GetDoubleOrZero(nonRem), csv.GetDoubleOrZero(entropy));
            }
        }

        writer.Flush();
        context.AddRows(writer.RowCount);
    }

    private static async Task ImportHrvDetailAsync(ImportContext context, SqliteConnection connection, string folder, CancellationToken ct)
    {
        using var writer = new SqliteBulkWriter(
            connection,
            "INSERT OR REPLACE INTO HrvDetails (TimestampUtc, RmssdMs, StdDevMs) VALUES ($timestamp, $rmssd, $stdDev)",
            "$timestamp", "$rmssd", "$stdDev");

        await ParallelCsv.RunAsync(
            Directory.GetFiles(folder, "heart_rate_variability_*.csv"),
            ParseHrvDetailFile,
            rows =>
            {
                foreach (var (timestamp, rmssd, stdDev) in rows)
                {
                    writer.Write(timestamp, rmssd, stdDev);
                }
            },
            null,
            ct);

        writer.Flush();
        context.AddRows(writer.RowCount);
    }

    private static List<(DateTime Timestamp, double Rmssd, double StdDev)> ParseHrvDetailFile(string file)
    {
        var rows = new List<(DateTime, double, double)>();
        using var csv = CsvCursor.Open(file);
        if (csv is null)
        {
            return rows;
        }

        var timestamp = csv.Column("timestamp");
        var rmssd = csv.Column("root mean square of successive differences milliseconds");
        var stdDev = csv.Column("standard deviation milliseconds");
        while (timestamp >= 0 && csv.NextRow())
        {
            if (csv.GetUtc(timestamp) is { } utc)
            {
                rows.Add((utc, csv.GetDoubleOrZero(rmssd), csv.GetDoubleOrZero(stdDev)));
            }
        }

        return rows;
    }

    private static async Task ImportIntradayHeartRateAsync(ImportContext context, SqliteConnection connection, string folder, CancellationToken ct)
    {
        var files = Directory.EnumerateFiles(folder, "heart_rate_*.csv")
            .Where(f => !Path.GetFileName(f).Contains("variability", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToArray();

        using var writer = new SqliteBulkWriter(
            connection,
            "INSERT OR REPLACE INTO HeartRateMinutelies (MinuteUtc, MinBpm, AvgBpm, MaxBpm, SampleCount) VALUES ($minute, $min, $avg, $max, $count)",
            "$minute", "$min", "$avg", "$max", "$count");

        await ParallelCsv.RunAsync(
            files,
            ParseMinuteBuckets,
            buckets =>
            {
                foreach (var (minute, bucket) in buckets)
                {
                    writer.Write(minute, bucket.Min, bucket.Sum / bucket.Count, bucket.Max, bucket.Count);
                }
            },
            done =>
            {
                if (done % 25 == 0 || done == files.Length)
                {
                    context.ReportProgress($"Herzfrequenz-Verlauf wird aggregiert ({done}/{files.Length} Tage)...", 40 + (55 * done / files.Length));
                }
            },
            ct);

        writer.Flush();
        context.AddRows(writer.RowCount);
    }

    /// <summary>
    /// Collapses one day of the continuous heart rate stream into min/avg/max per minute. Nothing in
    /// the app plots raw samples, and this is what keeps the table at a few hundred thousand rows
    /// rather than tens of millions.
    /// </summary>
    private static Dictionary<DateTime, MinuteBucket> ParseMinuteBuckets(string file)
    {
        var buckets = new Dictionary<DateTime, MinuteBucket>(1440);
        using var csv = CsvCursor.Open(file);
        if (csv is null)
        {
            return buckets;
        }

        var timestamp = csv.Column("timestamp");
        var bpmColumn = csv.Column("beats per minute");
        while (timestamp >= 0 && bpmColumn >= 0 && csv.NextRow())
        {
            if (csv.GetUtc(timestamp) is not { } utc || !csv.TryGetDouble(bpmColumn, out var bpm))
            {
                continue;
            }

            var minute = new DateTime(utc.Ticks - (utc.Ticks % TimeSpan.TicksPerMinute), DateTimeKind.Utc);
            ref var bucket = ref CollectionsMarshal.GetValueRefOrAddDefault(buckets, minute, out var existed);
            if (existed)
            {
                bucket.Min = Math.Min(bucket.Min, bpm);
                bucket.Max = Math.Max(bucket.Max, bpm);
                bucket.Sum += bpm;
                bucket.Count++;
            }
            else
            {
                bucket = new MinuteBucket { Min = bpm, Max = bpm, Sum = bpm, Count = 1 };
            }
        }

        return buckets;
    }

    private struct MinuteBucket
    {
        public double Min;
        public double Max;
        public double Sum;
        public int Count;
    }
}
