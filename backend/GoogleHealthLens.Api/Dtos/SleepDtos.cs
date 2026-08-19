namespace GoogleHealthLens.Api.Dtos;

public record SleepStageDto(string StageType, DateTime StartUtc, DateTime EndUtc);

public record SleepScoreDto(
    double OverallScore,
    double? DurationScore,
    double? CompositionScore,
    double? RevitalizationScore,
    double DeepSleepMinutes,
    double RemSleepPercent,
    double? RestingHeartRate,
    double? RestlessnessNormalized);

public record SleepSessionListItemDto(
    string Id,
    DateTime StartUtc,
    DateTime EndUtc,
    string SleepType,
    int MinutesAsleep,
    int MinutesAwake,
    int TimeInBedMinutes,
    double? OverallScore,
    bool IsLegacy);

public record SleepSessionDetailDto(
    string Id,
    DateTime StartUtc,
    DateTime EndUtc,
    string SleepType,
    string? DataSource,
    int MinutesAsleep,
    int MinutesAwake,
    int MinutesToFallAsleep,
    int MinutesAfterWakeup,
    int TimeInBedMinutes,
    double? EfficiencyPercent,
    bool IsLegacy,
    IReadOnlyList<SleepStageDto> Stages,
    SleepScoreDto? Score);

public record SleepSummaryDto(
    DateOnly From,
    DateOnly To,
    int Nights,
    double AvgMinutesAsleep,
    double AvgTimeInBedMinutes,
    double AvgEfficiencyPercent,
    double? AvgOverallScore,
    IReadOnlyList<SleepSessionListItemDto> Sessions);
