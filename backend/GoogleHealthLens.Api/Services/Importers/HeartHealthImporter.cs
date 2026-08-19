using System.Globalization;
using CsvHelper;
using GoogleHealthLens.Api.Models;
using GoogleHealthLens.Api.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GoogleHealthLens.Api.Services.Importers;

/// <summary>
/// Resting HR, AZM zone minutes, HRV (daily + 5-min sleep detail), respiratory rate, and a
/// curated 1-minute aggregate of the continuous intraday heart rate stream (which is by far the
/// largest source in this export — imported via raw batched ADO.NET, not EF change tracking).
/// </summary>
public class HeartHealthImporter : IDomainImporter
{
    private static readonly TimeZoneInfo LocalZone = ResolveLocalZone();

    public string Name => "Herzfrequenz & HRV";

    public async Task ImportAsync(ImportContext context, CancellationToken ct)
    {
        var pa = context.FindFolder("Physical Activity_GoogleData");
        if (pa is null)
        {
            return;
        }

        context.ReportProgress("Ruhepuls wird gelesen...", 0);
        await ImportSimpleDaily(context, Path.Combine(pa, "daily_resting_heart_rate.csv"), "beats per minute",
            async (db, date, value, token) =>
            {
                var existing = await db.RestingHeartRateDailies.FindAsync([date], token);
                if (existing is null)
                {
                    db.RestingHeartRateDailies.Add(new RestingHeartRateDaily { Date = date, Bpm = value });
                }
                else
                {
                    existing.Bpm = value;
                }
            }, ct);

        context.ReportProgress("Active Zone Minutes werden gelesen...", 10);
        await ImportAzm(context, pa, ct);

        context.ReportProgress("HRV-Tageswerte werden gelesen...", 20);
        await ImportHrvDaily(context, pa, ct);

        context.ReportProgress("HRV-Details werden gelesen...", 30);
        await ImportHrvDetail(context, pa, ct);

        context.ReportProgress("Atemfrequenz wird gelesen...", 35);
        await ImportSimpleDaily(context, Path.Combine(pa, "daily_respiratory_rate.csv"), "breaths per minute",
            async (db, date, value, token) =>
            {
                var existing = await db.RespiratoryRateDailies.FindAsync([date], token);
                if (existing is null)
                {
                    db.RespiratoryRateDailies.Add(new RespiratoryRateDaily { Date = date, BreathsPerMinute = value });
                }
                else
                {
                    existing.BreathsPerMinute = value;
                }
            }, ct);

        context.ReportProgress("Herzfrequenz-Verlauf wird aggregiert (das kann etwas dauern)...", 40);
        await ImportIntradayHeartRate(context, pa, ct);

        context.ReportProgress("Herzfrequenz & HRV importiert", 100);
    }

    private async Task ImportSimpleDaily(ImportContext context, string path, string valueColumn, Func<AppDbContext, DateOnly, double, CancellationToken, Task> upsert, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return;
        }

        await using var db = context.CreateContext();

        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        csv.Read();
        csv.ReadHeader();
        long count = 0;
        while (csv.Read())
        {
            var ts = ParseUtc(csv.GetField("timestamp"));
            if (ts is null || !double.TryParse(csv.GetField(valueColumn), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                continue;
            }

            await upsert(db, DateOnly.FromDateTime(ts.Value), value, ct);
            count++;
        }

        await db.SaveChangesAsync(ct);
        context.RowsImported += count;
    }

    private static async Task ImportAzm(ImportContext context, string folder, CancellationToken ct)
    {
        var totals = new Dictionary<(DateOnly date, string zone), int>();
        foreach (var file in Directory.EnumerateFiles(folder, "active_zone_minutes_*.csv"))
        {
            using var reader = new StreamReader(file);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            csv.Read();
            csv.ReadHeader();
            while (csv.Read())
            {
                var ts = ParseUtc(csv.GetField("timestamp"));
                var zone = csv.GetField("heart rate zone");
                if (ts is null || string.IsNullOrEmpty(zone) || !int.TryParse(csv.GetField("total minutes"), out var minutes))
                {
                    continue;
                }

                var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(ts.Value, LocalZone));
                var key = (date, zone);
                totals[key] = totals.GetValueOrDefault(key) + minutes;
            }
        }

        await using var db = context.CreateContext();
        foreach (var ((date, zone), minutes) in totals)
        {
            var existing = await db.HeartRateZoneMinutesDailies.FindAsync([date, zone], ct);
            if (existing is null)
            {
                db.HeartRateZoneMinutesDailies.Add(new HeartRateZoneMinutesDaily { Date = date, Zone = zone, Minutes = minutes });
            }
            else
            {
                existing.Minutes = minutes;
            }
        }

        await db.SaveChangesAsync(ct);
        context.RowsImported += totals.Count;
    }

    private static async Task ImportHrvDaily(ImportContext context, string folder, CancellationToken ct)
    {
        var path = Path.Combine(folder, "daily_heart_rate_variability.csv");
        if (!File.Exists(path))
        {
            return;
        }

        await using var db = context.CreateContext();
        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        csv.Read();
        csv.ReadHeader();
        long count = 0;
        while (csv.Read())
        {
            var ts = ParseUtc(csv.GetField("timestamp"));
            if (ts is null)
            {
                continue;
            }

            var date = DateOnly.FromDateTime(ts.Value);
            var existing = await db.HrvDailySummaries.FindAsync([date], ct);
            if (existing is not null)
            {
                continue;
            }

            db.HrvDailySummaries.Add(new HrvDailySummary
            {
                Date = date,
                RmssdMs = ParseDoubleOrZero(csv.GetField("average heart rate variability milliseconds")),
                NonRemHrBpm = ParseDoubleOrZero(csv.GetField("non rem heart rate beats per minute")),
                Entropy = ParseDoubleOrZero(csv.GetField("entropy")),
            });
            count++;
        }

        await db.SaveChangesAsync(ct);
        context.RowsImported += count;
    }

    private static async Task ImportHrvDetail(ImportContext context, string folder, CancellationToken ct)
    {
        await using var db = context.CreateContext();
        var conn = (SqliteConnection)db.Database.GetDbConnection();
        await conn.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = (SqliteTransaction)tx;
        cmd.CommandText = "INSERT OR IGNORE INTO HrvDetails (TimestampUtc, RmssdMs, StdDevMs) VALUES ($ts, $rmssd, $stddev)";
        var pTs = cmd.CreateParameter(); pTs.ParameterName = "$ts"; cmd.Parameters.Add(pTs);
        var pRmssd = cmd.CreateParameter(); pRmssd.ParameterName = "$rmssd"; cmd.Parameters.Add(pRmssd);
        var pStdDev = cmd.CreateParameter(); pStdDev.ParameterName = "$stddev"; cmd.Parameters.Add(pStdDev);

        long count = 0;
        foreach (var file in Directory.EnumerateFiles(folder, "heart_rate_variability_*.csv"))
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

                pTs.Value = ts.Value;
                pRmssd.Value = ParseDoubleOrZero(csv.GetField("root mean square of successive differences milliseconds"));
                pStdDev.Value = ParseDoubleOrZero(csv.GetField("standard deviation milliseconds"));
                await cmd.ExecuteNonQueryAsync(ct);
                count++;
            }
        }

        await tx.CommitAsync(ct);
        context.RowsImported += count;
    }

    private static async Task ImportIntradayHeartRate(ImportContext context, string folder, CancellationToken ct)
    {
        var files = Directory.EnumerateFiles(folder, "heart_rate_*.csv")
            .Where(f => !Path.GetFileName(f).Contains("variability", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f)
            .ToList();

        if (files.Count == 0)
        {
            return;
        }

        await using var db = context.CreateContext();
        var conn = (SqliteConnection)db.Database.GetDbConnection();
        await conn.OpenAsync(ct);
        using (var pragma = conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA synchronous = OFF;";
            await pragma.ExecuteNonQueryAsync(ct);
        }

        long totalRows = 0;
        for (var i = 0; i < files.Count; i++)
        {
            var minuteBuckets = new Dictionary<DateTime, (double min, double max, double sum, int count)>();

            using (var reader = new StreamReader(files[i]))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Read();
                csv.ReadHeader();
                while (csv.Read())
                {
                    var ts = ParseUtc(csv.GetField("timestamp"));
                    if (ts is null || !double.TryParse(csv.GetField("beats per minute"), NumberStyles.Float, CultureInfo.InvariantCulture, out var bpm))
                    {
                        continue;
                    }

                    var minute = new DateTime(ts.Value.Year, ts.Value.Month, ts.Value.Day, ts.Value.Hour, ts.Value.Minute, 0, DateTimeKind.Utc);
                    if (minuteBuckets.TryGetValue(minute, out var bucket))
                    {
                        minuteBuckets[minute] = (Math.Min(bucket.min, bpm), Math.Max(bucket.max, bpm), bucket.sum + bpm, bucket.count + 1);
                    }
                    else
                    {
                        minuteBuckets[minute] = (bpm, bpm, bpm, 1);
                    }
                }
            }

            await using var tx = await conn.BeginTransactionAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = (SqliteTransaction)tx;
            cmd.CommandText = "INSERT OR IGNORE INTO HeartRateMinutelies (MinuteUtc, MinBpm, AvgBpm, MaxBpm, SampleCount) VALUES ($minute, $min, $avg, $max, $count)";
            var pMinute = cmd.CreateParameter(); pMinute.ParameterName = "$minute"; cmd.Parameters.Add(pMinute);
            var pMin = cmd.CreateParameter(); pMin.ParameterName = "$min"; cmd.Parameters.Add(pMin);
            var pAvg = cmd.CreateParameter(); pAvg.ParameterName = "$avg"; cmd.Parameters.Add(pAvg);
            var pMax = cmd.CreateParameter(); pMax.ParameterName = "$max"; cmd.Parameters.Add(pMax);
            var pCount = cmd.CreateParameter(); pCount.ParameterName = "$count"; cmd.Parameters.Add(pCount);

            foreach (var (minute, bucket) in minuteBuckets)
            {
                pMinute.Value = minute;
                pMin.Value = bucket.min;
                pAvg.Value = bucket.sum / bucket.count;
                pMax.Value = bucket.max;
                pCount.Value = bucket.count;
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            totalRows += minuteBuckets.Count;

            if (i % 25 == 0)
            {
                var pct = 40 + (int)(55.0 * i / files.Count);
                context.ReportProgress($"Herzfrequenz-Verlauf wird aggregiert ({i}/{files.Count} Tage)...", pct);
            }
        }

        context.RowsImported += totalRows;
    }

    private static double ParseDoubleOrZero(string? s) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;

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
}
