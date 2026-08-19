namespace HealthLens.Api.Models;

/// <summary>One row per calendar date, aggregated from steps_/distance_/calories_/active_minutes_ monthly CSVs.</summary>
public class DailyActivitySummary
{
    public DateOnly Date { get; set; }
    public int? Steps { get; set; }
    public double? DistanceMeters { get; set; }
    public double? CaloriesTotal { get; set; }
    public double? CaloriesActive { get; set; }
    public int? ActiveMinutes { get; set; }
    public int? SedentaryMinutes { get; set; }
}
