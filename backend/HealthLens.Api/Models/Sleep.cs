namespace HealthLens.Api.Models;

/// <summary>One sleep session, keyed by Fitbit's sleep_id (modern) or logId (legacy gap-fill).</summary>
public class SleepSession
{
    public long Id { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public string SleepType { get; set; } = "";
    public string? DataSource { get; set; }
    public int MinutesAsleep { get; set; }
    public int MinutesAwake { get; set; }
    public int MinutesToFallAsleep { get; set; }
    public int MinutesAfterWakeup { get; set; }
    public int TimeInBedMinutes { get; set; }
    public double? EfficiencyPercent { get; set; }
    public bool IsLegacy { get; set; }

    public List<SleepStage> Stages { get; set; } = [];
    public SleepScore? Score { get; set; }
}

/// <summary>One stage interval within a sleep session (AWAKE/LIGHT/DEEP/REM for modern "stages" sleep, RESTLESS/AWAKE/ASLEEP for legacy "classic" sleep).</summary>
public class SleepStage
{
    public int Id { get; set; }
    public long SleepSessionId { get; set; }
    public string StageType { get; set; } = "";
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
}

/// <summary>1:1 child of SleepSession — only present for nights Fitbit scored.</summary>
public class SleepScore
{
    public long SleepSessionId { get; set; }
    public double OverallScore { get; set; }
    public double? DurationScore { get; set; }
    public double? CompositionScore { get; set; }
    public double? RevitalizationScore { get; set; }
    public double DeepSleepMinutes { get; set; }
    public double RemSleepPercent { get; set; }
    public double? RestingHeartRate { get; set; }
    public double? RestlessnessNormalized { get; set; }
}
