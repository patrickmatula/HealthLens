namespace GoogleHealthLens.Api.Models;

public class RestingHeartRateDaily
{
    public DateOnly Date { get; set; }
    public double Bpm { get; set; }
}

public class HeartRateZoneMinutesDaily
{
    public DateOnly Date { get; set; }
    public string Zone { get; set; } = "";
    public int Minutes { get; set; }
}

public class HrvDailySummary
{
    public DateOnly Date { get; set; }
    public double RmssdMs { get; set; }
    public double NonRemHrBpm { get; set; }
    public double Entropy { get; set; }
}

/// <summary>5-minute-granularity HRV samples, only present during sleep — low volume, always kept raw.</summary>
public class HrvDetail
{
    public DateTime TimestampUtc { get; set; }
    public double RmssdMs { get; set; }
    public double StdDevMs { get; set; }
}

public class RespiratoryRateDaily
{
    public DateOnly Date { get; set; }
    public double BreathsPerMinute { get; set; }
}

/// <summary>Curated aggregate of the continuous intraday heart rate stream — one row per minute that has data.</summary>
public class HeartRateMinutely
{
    public DateTime MinuteUtc { get; set; }
    public double MinBpm { get; set; }
    public double AvgBpm { get; set; }
    public double MaxBpm { get; set; }
    public int SampleCount { get; set; }
}
