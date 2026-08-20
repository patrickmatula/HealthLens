using System.Text.Json;
using HealthLens.Api.Models;
using HealthLens.Api.Services.Csv;
using HealthLens.Api.Services.Import;
using Microsoft.Data.Sqlite;

namespace HealthLens.Api.Services.Importers;

/// <summary>
/// Builds SleepSessions/SleepStages/SleepScores from the modern UserSleeps/UserSleepStages/
/// UserSleepScores snapshots, gap-filled with the legacy Fitbit sleep-*.json export for nights the
/// modern pipeline doesn't cover (this account's modern coverage is sparse — most history lives in
/// the legacy export). The legacy JSON files are parsed in parallel; stage rows, which run to a
/// six-figure count once the legacy nights are included, are written with batched raw SQL.
/// </summary>
public sealed class SleepImporter : IDomainImporter
{
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
            await ImportLegacySleepAsync(legacy, sessions, stages, ct);
        }

        context.ReportProgress("Schlafdaten werden gespeichert...", 85);
        Save(context, sessions, stages, scores);
        context.ReportProgress("Schlafdaten importiert", 100);
    }

    private static void Save(
        ImportContext context,
        Dictionary<long, SleepSession> sessions,
        Dictionary<long, List<SleepStage>> stages,
        Dictionary<long, SleepScore> scores)
    {
        using var connection = context.OpenWriteConnection();

        using (var writer = new SqliteBulkWriter(
            connection,
            """
            INSERT OR REPLACE INTO SleepSessions
                (Id, StartUtc, EndUtc, SleepType, DataSource, MinutesAsleep, MinutesAwake, MinutesToFallAsleep, MinutesAfterWakeup, TimeInBedMinutes, EfficiencyPercent, IsLegacy)
            VALUES ($id, $start, $end, $type, $source, $asleep, $awake, $toFallAsleep, $afterWakeup, $inBed, $efficiency, $isLegacy)
            """,
            "$id", "$start", "$end", "$type", "$source", "$asleep", "$awake", "$toFallAsleep", "$afterWakeup", "$inBed", "$efficiency", "$isLegacy"))
        {
            foreach (var s in sessions.Values)
            {
                writer.Write(s.Id, s.StartUtc, s.EndUtc, s.SleepType, s.DataSource, s.MinutesAsleep, s.MinutesAwake,
                    s.MinutesToFallAsleep, s.MinutesAfterWakeup, s.TimeInBedMinutes, s.EfficiencyPercent, s.IsLegacy);
            }

            writer.Flush();
            context.AddRows(writer.RowCount);
        }

        // Stages have a surrogate key, so a re-import can't upsert them — replace each session's set
        // wholesale, which also means a corrected export actually refreshes them.
        DeleteChildren(connection, "DELETE FROM SleepStages WHERE SleepSessionId = $id", stages.Keys);

        using (var writer = new SqliteBulkWriter(
            connection,
            "INSERT INTO SleepStages (SleepSessionId, StageType, StartUtc, EndUtc) VALUES ($id, $type, $start, $end)",
            "$id", "$type", "$start", "$end"))
        {
            foreach (var (sessionId, list) in stages)
            {
                foreach (var stage in list)
                {
                    writer.Write(sessionId, stage.StageType, stage.StartUtc, stage.EndUtc);
                }
            }

            writer.Flush();
            context.AddRows(writer.RowCount);
        }

        using (var writer = new SqliteBulkWriter(
            connection,
            """
            INSERT OR REPLACE INTO SleepScores
                (SleepSessionId, OverallScore, DurationScore, CompositionScore, RevitalizationScore, DeepSleepMinutes, RemSleepPercent, RestingHeartRate, RestlessnessNormalized)
            VALUES ($id, $overall, $duration, $composition, $revitalization, $deep, $rem, $restingHr, $restlessness)
            """,
            "$id", "$overall", "$duration", "$composition", "$revitalization", "$deep", "$rem", "$restingHr", "$restlessness"))
        {
            foreach (var (sessionId, score) in scores)
            {
                if (!sessions.ContainsKey(sessionId))
                {
                    continue;
                }

                writer.Write(sessionId, score.OverallScore, score.DurationScore, score.CompositionScore, score.RevitalizationScore,
                    score.DeepSleepMinutes, score.RemSleepPercent, score.RestingHeartRate, score.RestlessnessNormalized);
            }

            writer.Flush();
            context.AddRows(writer.RowCount);
        }
    }

    internal static void DeleteChildren(SqliteConnection connection, string sql, IEnumerable<long> parentIds)
    {
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$id";
        command.Parameters.Add(parameter);

        foreach (var id in parentIds)
        {
            parameter.Value = id;
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    // ---- Modern (UserSleeps / UserSleepStages / UserSleepScores) ----------

    private static void ParseUserSleeps(string folder, Dictionary<long, SleepSession> sessions)
    {
        foreach (var file in Directory.EnumerateFiles(folder, "UserSleeps_*.csv").Order(StringComparer.Ordinal))
        {
            using var csv = CsvCursor.Open(file);
            if (csv is null)
            {
                continue;
            }

            var id = csv.Column("sleep_id");
            var start = csv.Column("sleep_start");
            var end = csv.Column("sleep_end");
            var type = csv.Column("sleep_type");
            var dataSource = csv.Column("data_source");
            var asleep = csv.Column("minutes_asleep");
            var awake = csv.Column("minutes_awake");
            var toFallAsleep = csv.Column("minutes_to_fall_asleep");
            var afterWakeup = csv.Column("minutes_after_wake_up");
            var inBed = csv.Column("minutes_in_sleep_period");

            while (csv.NextRow())
            {
                if (csv.GetInt64(id) is not { } sleepId || csv.GetUtc(start) is not { } startUtc || csv.GetUtc(end) is not { } endUtc)
                {
                    continue;
                }

                sessions[sleepId] = new SleepSession
                {
                    Id = sleepId,
                    StartUtc = startUtc,
                    EndUtc = endUtc,
                    SleepType = csv.GetString(type),
                    DataSource = csv.GetString(dataSource),
                    MinutesAsleep = csv.GetInt32OrZero(asleep),
                    MinutesAwake = csv.GetInt32OrZero(awake),
                    MinutesToFallAsleep = csv.GetInt32OrZero(toFallAsleep),
                    MinutesAfterWakeup = csv.GetInt32OrZero(afterWakeup),
                    TimeInBedMinutes = csv.GetInt32OrZero(inBed),
                };
            }
        }
    }

    private static void ParseUserSleepStages(string folder, Dictionary<long, List<SleepStage>> stages)
    {
        foreach (var file in Directory.EnumerateFiles(folder, "UserSleepStages_*.csv").Order(StringComparer.Ordinal))
        {
            using var csv = CsvCursor.Open(file);
            if (csv is null)
            {
                continue;
            }

            var id = csv.Column("sleep_id");
            var type = csv.Column("sleep_stage_type");
            var start = csv.Column("sleep_stage_start");
            var end = csv.Column("sleep_stage_end");

            while (csv.NextRow())
            {
                if (csv.GetInt64(id) is not { } sleepId || csv.GetUtc(start) is not { } startUtc || csv.GetUtc(end) is not { } endUtc)
                {
                    continue;
                }

                if (!stages.TryGetValue(sleepId, out var list))
                {
                    list = [];
                    stages[sleepId] = list;
                }

                list.Add(new SleepStage { SleepSessionId = sleepId, StageType = csv.GetString(type), StartUtc = startUtc, EndUtc = endUtc });
            }
        }
    }

    private static void ParseUserSleepScores(string folder, Dictionary<long, SleepScore> scores)
    {
        foreach (var file in Directory.EnumerateFiles(folder, "UserSleepScores_*.csv").Order(StringComparer.Ordinal))
        {
            using var csv = CsvCursor.Open(file);
            if (csv is null)
            {
                continue;
            }

            var id = csv.Column("sleep_id");
            var overall = csv.Column("overall_score");
            var duration = csv.Column("duration_score");
            var composition = csv.Column("composition_score");
            var revitalization = csv.Column("revitalization_score");
            var deep = csv.Column("deep_sleep_minutes");
            var rem = csv.Column("rem_sleep_percent");
            var restingHr = csv.Column("resting_heart_rate");
            var restlessness = csv.Column("restlessness_normalized");

            while (csv.NextRow())
            {
                if (csv.GetInt64(id) is not { } sleepId)
                {
                    continue;
                }

                scores[sleepId] = new SleepScore
                {
                    SleepSessionId = sleepId,
                    OverallScore = csv.GetDoubleOrZero(overall),
                    DurationScore = PositiveOrNull(csv.GetDouble(duration)),
                    CompositionScore = PositiveOrNull(csv.GetDouble(composition)),
                    RevitalizationScore = PositiveOrNull(csv.GetDouble(revitalization)),
                    DeepSleepMinutes = csv.GetDoubleOrZero(deep),
                    RemSleepPercent = csv.GetDoubleOrZero(rem),
                    RestingHeartRate = csv.GetDouble(restingHr),
                    RestlessnessNormalized = csv.GetDouble(restlessness),
                };
            }
        }
    }

    // ---- Legacy (sleep-*.json) ---------------------------------------------

    private static async Task ImportLegacySleepAsync(
        string folder,
        Dictionary<long, SleepSession> sessions,
        Dictionary<long, List<SleepStage>> stages,
        CancellationToken ct)
    {
        var modern = new IntervalIndex();
        foreach (var session in sessions.Values)
        {
            modern.Add(session.StartUtc, session.EndUtc);
        }

        await ParallelCsv.RunAsync(
            Directory.GetFiles(folder, "sleep-*.json"),
            ParseLegacyFile,
            nights =>
            {
                foreach (var (session, nightStages) in nights)
                {
                    if (sessions.ContainsKey(session.Id) || modern.Overlaps(session.StartUtc, session.EndUtc))
                    {
                        continue;
                    }

                    sessions[session.Id] = session;
                    if (nightStages.Count > 0)
                    {
                        stages[session.Id] = nightStages;
                    }
                }
            },
            null,
            ct);
    }

    private static List<(SleepSession Session, List<SleepStage> Stages)> ParseLegacyFile(string file)
    {
        var nights = new List<(SleepSession, List<SleepStage>)>();

        using var stream = File.OpenRead(file);
        using var document = JsonDocument.Parse(stream);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return nights;
        }

        foreach (var entry in document.RootElement.EnumerateArray())
        {
            if (!entry.TryGetProperty("logId", out var idProperty) ||
                ParseLegacyLocalTime(entry, "startTime") is not { } start ||
                ParseLegacyLocalTime(entry, "endTime") is not { } end)
            {
                continue;
            }

            var id = idProperty.GetInt64();
            var session = new SleepSession
            {
                Id = id,
                StartUtc = start,
                EndUtc = end,
                SleepType = LegacyJson.String(entry, "type") ?? "classic",
                DataSource = "Legacy Export",
                MinutesAsleep = (int)(LegacyJson.Number(entry, "minutesAsleep") ?? 0),
                MinutesAwake = (int)(LegacyJson.Number(entry, "minutesAwake") ?? 0),
                MinutesToFallAsleep = (int)(LegacyJson.Number(entry, "minutesToFallAsleep") ?? 0),
                MinutesAfterWakeup = (int)(LegacyJson.Number(entry, "minutesAfterWakeup") ?? 0),
                TimeInBedMinutes = (int)(LegacyJson.Number(entry, "timeInBed") ?? 0),
                EfficiencyPercent = LegacyJson.Number(entry, "efficiency"),
                IsLegacy = true,
            };

            var nightStages = new List<SleepStage>();
            if (entry.TryGetProperty("levels", out var levels) &&
                levels.TryGetProperty("data", out var data) &&
                data.ValueKind == JsonValueKind.Array)
            {
                foreach (var point in data.EnumerateArray())
                {
                    var seconds = (int)(LegacyJson.Number(point, "seconds") ?? 0);
                    if (seconds <= 0 || ParseLegacyLocalTime(point, "dateTime") is not { } stageStart)
                    {
                        continue;
                    }

                    nightStages.Add(new SleepStage
                    {
                        SleepSessionId = id,
                        StageType = (LegacyJson.String(point, "level") ?? "").ToUpperInvariant(),
                        StartUtc = stageStart,
                        EndUtc = stageStart.AddSeconds(seconds),
                    });
                }
            }

            nights.Add((session, nightStages));
        }

        return nights;
    }

    private static DateTime? ParseLegacyLocalTime(JsonElement element, string property) =>
        LegacyJson.String(element, property) is { } raw && DateTime.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var local)
            ? LocalTimeZone.ToUtc(local)
            : null;

    private static double? PositiveOrNull(double? value) => value is null or < 0 ? null : value;
}
