namespace HealthLens.Api.Dtos;

// Ids are Fitbit's own 64-bit exercise_id values, which exceed JS's safe integer range (2^53) —
// serialize as strings so the frontend never round-trips them through a JSON number.
public record WorkoutListItemDto(
    string Id,
    DateTime StartUtc,
    DateTime EndUtc,
    double DurationSeconds,
    string ActivityName,
    double? DistanceMeters,
    double? Calories,
    double? AvgHeartRate,
    double? AvgPaceSecPerKm,
    double? CadenceAvgSpm,
    bool HasGps,
    bool IsLegacy,
    int? ShoeId,
    string? ShoeName);

public record WorkoutSplitDto(
    int SplitIndex,
    string Type,
    DateTime Timestamp,
    long ElapsedMs,
    double DistanceMeters,
    double Calories,
    int Steps,
    double? AvgHeartRate,
    double ElevationGainMeters,
    double? AvgSpeedKmh);

public record WorkoutSampleDto(
    DateTime Timestamp,
    double? Latitude,
    double? Longitude,
    double? AltitudeMeters,
    double? HeartRateBpm,
    double? PaceSecPerKm,
    double? CadenceSpm,
    double? SpeedKmh,
    double? StrideLengthCm,
    double? VerticalOscillationCm,
    double? VerticalRatioPercent,
    double? GroundContactTimeMs);

public record KmSplitDto(int Km, double DurationSeconds, double? AvgHeartRate);

public record WorkoutDetailDto(
    string Id,
    DateTime StartUtc,
    DateTime EndUtc,
    double DurationSeconds,
    string ActivityName,
    string LogType,
    string? Source,
    double? DistanceMeters,
    double? Calories,
    int? Steps,
    double? AvgHeartRate,
    double? PeakHeartRate,
    double? AvgPaceSecPerKm,
    double? AvgSpeedKmh,
    double? PeakSpeedKmh,
    double? ElevationGainMeters,
    double? CardioLoad,
    double? CadenceAvgSpm,
    double? GroundContactTimeMs,
    double? VerticalOscillationMm,
    double? VerticalRatioPercent,
    double? RatePerceivedExertion,
    bool HasGps,
    bool IsLegacy,
    int? ShoeId,
    string? ShoeName,
    IReadOnlyList<WorkoutSplitDto> Splits,
    IReadOnlyList<KmSplitDto> KmSplits,
    IReadOnlyList<WorkoutSampleDto> Samples,
    IReadOnlyList<PersonalRecordDto> PersonalRecords);

public record PersonalRecordDto(
    int Id,
    string? WorkoutId,
    string NameLocalizationId,
    string State,
    DateTime AchieveTimeUtc,
    double RecordValue,
    string RecordType,
    double? ExtentValueMeters,
    string? WorkoutActivityName);
