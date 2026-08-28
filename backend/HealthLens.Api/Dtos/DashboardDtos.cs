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
