namespace HealthLens.Api.Dtos;

public record DailyActivityPointDto(
    DateOnly Date,
    int? Steps,
    double? DistanceMeters,
    double? CaloriesTotal,
    int? ActiveMinutes,
    int? SedentaryMinutes,
    double? RestingHeartRateBpm,
    double? SleepScore);

public record ActivityCategoryCountDto(string Category, int Count);

public record WorkoutLocationDto(string WorkoutId, double Latitude, double Longitude);

public record ConsistencyDayDto(DateOnly Date, int? Steps, int WorkoutCount);

public record FunFactsDto(double TotalDistanceMeters, double TotalElevationGainMeters, int TotalWorkouts);

public record FlashbackDto(bool HasFlashback, DateTime? WorkoutStartUtc, string? ActivityName, double? DistanceMeters, double? DurationSeconds, int? YearsAgo);

public record TrainingLoadDto(double AcuteLoadMinutes, double ChronicLoadMinutesPerWeek, double? Acwr, string Zone);

public record WeeklyDigestDto(
    DateOnly From,
    DateOnly To,
    double TotalDistanceMeters,
    int WorkoutsCount,
    int ActiveDaysCount,
    double TotalSleepMinutes,
    double SleepDebtMinutes,
    double? AvgSleepScore,
    double? AvgRestingHeartRate,
    double? RestingHrDeltaFromPriorWeek);

public record DashboardOverviewDto(
    DateOnly From,
    DateOnly To,
    int DaysWithData,
    long TotalSteps,
    double TotalDistanceMeters,
    double TotalCalories,
    double AvgStepsPerDay,
    double AvgActiveMinutesPerDay,
    IReadOnlyList<DailyActivityPointDto> Days,
    int WorkoutsInRange,
    double? AvgSleepScore,
    double? AvgRestingHeartRate,
    IReadOnlyList<string> Insights,
    IReadOnlyList<ActivityCategoryCountDto> ActivityBreakdown);
