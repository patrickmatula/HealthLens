using System.Globalization;
using System.Text.Json;
using CsvHelper;
using GoogleHealthLens.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GoogleHealthLens.Api.Services.Importers;

/// <summary>
/// Builds SleepSessions/SleepStages/SleepScores from the modern UserSleeps/UserSleepStages/
/// UserSleepScores snapshots, gap-filled with the legacy Fitbit sleep-*.json export for nights
/// the modern pipeline doesn't cover (this account's modern coverage is sparse — most history
/// lives in the legacy export).
/// </summary>
public class SleepImporter : IDomainImporter
{
    private static readonly TimeZoneInfo LocalZone = ResolveLocalZone();

    public string Name => "Schlaf";

    public async Task ImportAsync(ImportContext context, CancellationToken ct)
    {
        var hfd = context.FindFolder("Health Fitness Data_GoogleData");
        var legacy = context.FindFolder("Global Export Data");

        var sessions = new Dictionary<long, SleepSession>();
        var stages = new Dictionary<long, List<SleepStage>>();
        var scores = new Dictionary<long, SleepScore>();

        if (hfd is not null)
        {
            context.ReportProgress("Schlafsitzungen werden gelesen...", 0);
            ParseUserSleeps(hfd, sessions);

            context.ReportProgress("Schlafphasen werden gelesen...", 25);
            ParseUserSleepStages(hfd, stages);

            context.ReportProgress("Schlaf-Scores werden gelesen...", 45);
            ParseUserSleepScores(hfd, scores);
        }

        if (legacy is not null)
        {
            context.ReportProgress("Ältere Schlafdaten (Legacy-Export) werden ergänzt...", 60);
            ImportLegacySleep(legacy, sessions, stages);
        }

        context.ReportProgress("Schlafdaten werden gespeichert...", 85);
        await using var db = context.CreateContext();

        foreach (var session in sessions.Values)
        {
            var existing = await db.SleepSessions.FindAsync([session.Id], ct);
            if (existing is null)
            {
                db.SleepSessions.Add(session);
            }
            else
            {
                db.Entry(existing).CurrentValues.SetValues(session);
            }
        }

        await db.SaveChangesAsync(ct);

        foreach (var (sessionId, list) in stages)
        {
            if (await db.SleepStages.AnyAsync(s => s.SleepSessionId == sessionId, ct))
            {
                continue;
            }

            foreach (var stage in list)
            {
                stage.SleepSessionId = sessionId;
            }

            db.SleepStages.AddRange(list);
        }

        foreach (var (sessionId, score) in scores)
        {
            if (await db.SleepScores.FindAsync([sessionId], ct) is null)
            {
                score.SleepSessionId = sessionId;
                db.SleepScores.Add(score);
            }
        }

        await db.SaveChangesAsync(ct);
        context.RowsImported += sessions.Count + stages.Sum(s => s.Value.Count) + scores.Count;
        context.ReportProgress("Schlafdaten importiert", 100);
    }

    // ---- Modern (UserSleeps / UserSleepStages / UserSleepScores) ----------

    private static void ParseUserSleeps(string folder, Dictionary<long, SleepSession> sessions)
    {
        foreach (var file in Directory.EnumerateFiles(folder, "UserSleeps_*.csv").OrderBy(f => f))
        {
            using var reader = new StreamReader(file);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            csv.Read();
            csv.ReadHeader();
            while (csv.Read())
            {
                if (!long.TryParse(csv.GetField("sleep_id"), out var id))
                {
                    continue;
                }

                var start = ParseUtc(csv.GetField("sleep_start"));
                var end = ParseUtc(csv.GetField("sleep_end"));
                if (start is null || end is null)
                {
                    continue;
                }

                sessions[id] = new SleepSession
                {
                    Id = id,
                    StartUtc = start.Value,
                    EndUtc = end.Value,
                    SleepType = csv.GetField("sleep_type") ?? "",
                    DataSource = csv.GetField("data_source"),
                    MinutesAsleep = ParseIntOrZero(csv.GetField("minutes_asleep")),
                    MinutesAwake = ParseIntOrZero(csv.GetField("minutes_awake")),
                    MinutesToFallAsleep = ParseIntOrZero(csv.GetField("minutes_to_fall_asleep")),
                    MinutesAfterWakeup = ParseIntOrZero(csv.GetField("minutes_after_wake_up")),
                    TimeInBedMinutes = ParseIntOrZero(csv.GetField("minutes_in_sleep_period")),
                };
            }
        }
    }

    private static void ParseUserSleepStages(string folder, Dictionary<long, List<SleepStage>> stages)
    {
        foreach (var file in Directory.EnumerateFiles(folder, "UserSleepStages_*.csv").OrderBy(f => f))
        {
            using var reader = new StreamReader(file);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            csv.Read();
            csv.ReadHeader();
            while (csv.Read())
            {
                if (!long.TryParse(csv.GetField("sleep_id"), out var sleepId))
                {
                    continue;
                }

                var start = ParseUtc(csv.GetField("sleep_stage_start"));
                var end = ParseUtc(csv.GetField("sleep_stage_end"));
                if (start is null || end is null)
                {
                    continue;
                }

                if (!stages.TryGetValue(sleepId, out var list))
                {
                    list = [];
                    stages[sleepId] = list;
                }

                list.Add(new SleepStage { SleepSessionId = sleepId, StageType = csv.GetField("sleep_stage_type") ?? "", StartUtc = start.Value, EndUtc = end.Value });
            }
        }
    }

    private static void ParseUserSleepScores(string folder, Dictionary<long, SleepScore> scores)
    {
        foreach (var file in Directory.EnumerateFiles(folder, "UserSleepScores_*.csv").OrderBy(f => f))
        {
            using var reader = new StreamReader(file);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            csv.Read();
            csv.ReadHeader();
            while (csv.Read())
            {
                if (!long.TryParse(csv.GetField("sleep_id"), out var sleepId))
                {
                    continue;
                }

                scores[sleepId] = new SleepScore
                {
                    SleepSessionId = sleepId,
                    OverallScore = ParseNullableDouble(csv.GetField("overall_score")) ?? 0,
                    DurationScore = PositiveOrNull(ParseNullableDouble(csv.GetField("duration_score"))),
                    CompositionScore = PositiveOrNull(ParseNullableDouble(csv.GetField("composition_score"))),
                    RevitalizationScore = PositiveOrNull(ParseNullableDouble(csv.GetField("revitalization_score"))),
                    DeepSleepMinutes = ParseNullableDouble(csv.GetField("deep_sleep_minutes")) ?? 0,
                    RemSleepPercent = ParseNullableDouble(csv.GetField("rem_sleep_percent")) ?? 0,
                    RestingHeartRate = ParseNullableDouble(csv.GetField("resting_heart_rate")),
                    RestlessnessNormalized = ParseNullableDouble(csv.GetField("restlessness_normalized")),
                };
            }
        }
    }

    // ---- Legacy (sleep-*.json) ---------------------------------------------

    private static void ImportLegacySleep(string folder, Dictionary<long, SleepSession> sessions, Dictionary<long, List<SleepStage>> stages)
    {
        var modern = sessions.Values.ToList();

        foreach (var file in Directory.EnumerateFiles(folder, "sleep-*.json"))
        {
            using var stream = File.OpenRead(file);
            using var doc = JsonDocument.Parse(stream);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var entry in doc.RootElement.EnumerateArray())
            {
                if (!entry.TryGetProperty("logId", out var idProp) || !entry.TryGetProperty("startTime", out var startProp) || !entry.TryGetProperty("endTime", out var endProp))
                {
                    continue;
                }

                var id = idProp.GetInt64();
                if (sessions.ContainsKey(id))
                {
                    continue;
                }

                var start = ParseLegacyLocalTime(startProp.GetString());
                var end = ParseLegacyLocalTime(endProp.GetString());
                if (start is null || end is null)
                {
                    continue;
                }

                if (modern.Any(s => s.StartUtc < end && start.Value < s.EndUtc))
                {
                    continue;
                }

                sessions[id] = new SleepSession
                {
                    Id = id,
                    StartUtc = start.Value,
                    EndUtc = end.Value,
                    SleepType = entry.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "classic" : "classic",
                    DataSource = "Legacy Export",
                    MinutesAsleep = entry.TryGetProperty("minutesAsleep", out var mAsleep) ? mAsleep.GetInt32() : 0,
                    MinutesAwake = entry.TryGetProperty("minutesAwake", out var mAwake) ? mAwake.GetInt32() : 0,
                    MinutesToFallAsleep = entry.TryGetProperty("minutesToFallAsleep", out var mFall) ? mFall.GetInt32() : 0,
                    MinutesAfterWakeup = entry.TryGetProperty("minutesAfterWakeup", out var mAfter) ? mAfter.GetInt32() : 0,
                    TimeInBedMinutes = entry.TryGetProperty("timeInBed", out var tib) ? tib.GetInt32() : 0,
                    EfficiencyPercent = entry.TryGetProperty("efficiency", out var eff) ? eff.GetDouble() : null,
                    IsLegacy = true,
                };

                if (entry.TryGetProperty("levels", out var levels) && levels.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                {
                    var list = new List<SleepStage>();
                    foreach (var point in data.EnumerateArray())
                    {
                        var dt = point.TryGetProperty("dateTime", out var dtProp) ? ParseLegacyLocalTime(dtProp.GetString()) : null;
                        var seconds = point.TryGetProperty("seconds", out var secProp) ? secProp.GetInt32() : 0;
                        var level = point.TryGetProperty("level", out var levelProp) ? levelProp.GetString() ?? "" : "";
                        if (dt is null || seconds <= 0)
                        {
                            continue;
                        }

                        list.Add(new SleepStage { SleepSessionId = id, StageType = level.ToUpperInvariant(), StartUtc = dt.Value, EndUtc = dt.Value.AddSeconds(seconds) });
                    }

                    stages[id] = list;
                }
            }
        }
    }

    private static DateTime? ParseLegacyLocalTime(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (!DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return null;
        }

        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), LocalZone);
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

    private static int ParseIntOrZero(string? s) => int.TryParse(s, out var v) ? v : 0;

    private static double? PositiveOrNull(double? v) => v is null or < 0 ? null : v;

    private static double? ParseNullableDouble(string? s) =>
        !string.IsNullOrWhiteSpace(s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

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
}
