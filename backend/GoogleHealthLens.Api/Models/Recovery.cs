namespace GoogleHealthLens.Api.Models;

public class StressScoreDaily
{
    public DateOnly Date { get; set; }
    public double Score { get; set; }
    public string Status { get; set; } = "";
}

public class DailyReadiness
{
    public DateOnly Date { get; set; }
    public double Score { get; set; }
    public string Level { get; set; } = "";
    public string SleepReadiness { get; set; } = "";
    public string HrvReadiness { get; set; } = "";
    public string RestingHrReadiness { get; set; } = "";
}

public class SpO2Daily
{
    public DateOnly Date { get; set; }
    public double AveragePercent { get; set; }
    public double LowerBoundPercent { get; set; }
    public double UpperBoundPercent { get; set; }
}

public class TemperatureNightly
{
    public DateOnly Date { get; set; }
    public double NightlyCelsius { get; set; }
    public double? BaselineCelsius { get; set; }
}
