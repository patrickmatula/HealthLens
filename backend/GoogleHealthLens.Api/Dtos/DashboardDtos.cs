namespace GoogleHealthLens.Api.Dtos;

public record DailyActivityPointDto(DateOnly Date, int? Steps, double? DistanceMeters, double? CaloriesTotal, int? ActiveMinutes, int? SedentaryMinutes);

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
    IReadOnlyList<string> Insights);
