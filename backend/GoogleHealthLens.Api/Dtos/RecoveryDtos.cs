namespace GoogleHealthLens.Api.Dtos;

public record StressScorePointDto(DateOnly Date, double Score);

public record ReadinessPointDto(DateOnly Date, double Score, string Level);

public record SpO2PointDto(DateOnly Date, double AveragePercent, double LowerBoundPercent, double UpperBoundPercent);

public record TemperaturePointDto(DateOnly Date, double NightlyCelsius, double? BaselineCelsius, double? DeltaFromBaseline);

public record RecoveryOverviewDto(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<StressScorePointDto> StressScore,
    IReadOnlyList<ReadinessPointDto> Readiness,
    IReadOnlyList<SpO2PointDto> SpO2,
    IReadOnlyList<TemperaturePointDto> Temperature);

/// <summary>One night's sleep score paired with same-day resting HR/stress/readiness — the raw material for the correlation view.</summary>
public record CorrelationPointDto(DateOnly Date, double SleepScore, double? RestingHeartRate, double? StressScore, double? ReadinessScore);
