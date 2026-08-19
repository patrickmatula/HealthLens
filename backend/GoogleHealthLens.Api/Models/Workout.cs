namespace GoogleHealthLens.Api.Models;

/// <summary>One completed exercise session, keyed by Fitbit's own exercise_id (or a synthetic id for legacy gap-fill entries).</summary>
public class Workout
{
    public long Id { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public string ActivityName { get; set; } = "";
    public string LogType { get; set; } = "";
    public string? Source { get; set; }
    public double? DistanceMeters { get; set; }
    public double? Calories { get; set; }
    public int? Steps { get; set; }
    public double? AvgHeartRate { get; set; }
    public double? PeakHeartRate { get; set; }
    public double? AvgPaceSecPerKm { get; set; }
    public double? AvgSpeedKmh { get; set; }
    public double? PeakSpeedKmh { get; set; }
    public double? ElevationGainMeters { get; set; }
    public double? CardioLoad { get; set; }
    public double? CadenceAvgSpm { get; set; }
    public double? GroundContactTimeMs { get; set; }
    public double? VerticalOscillationMm { get; set; }
    public double? VerticalRatioPercent { get; set; }
    public double? RatePerceivedExertion { get; set; }
    public bool IsLegacy { get; set; }
    public bool HasGps { get; set; }

    public double DurationSeconds => (EndUtc - StartUtc).TotalSeconds;

    public List<WorkoutSplit> Splits { get; set; } = [];
}

/// <summary>One split/pause/stop event parsed out of UserExercises' embedded "events" column.</summary>
public class WorkoutSplit
{
    public int Id { get; set; }
    public long WorkoutId { get; set; }
    public int SplitIndex { get; set; }
    public string Type { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public long ElapsedMs { get; set; }
    public double DistanceMeters { get; set; }
    public double Calories { get; set; }
    public int Steps { get; set; }
    public double? AvgHeartRate { get; set; }
    public double ElevationGainMeters { get; set; }
    public double? AvgSpeedKmh { get; set; }
}

/// <summary>
/// One second of a workout, denormalized across every per-second stream that overlaps its time
/// window (GPS, pace, cadence, speed, stride length, vertical oscillation/ratio, ground contact
/// time, heart rate) — the source for every workout-detail chart and the route map.
/// </summary>
public class WorkoutSample
{
    public long WorkoutId { get; set; }
    public DateTime Timestamp { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? AltitudeMeters { get; set; }
    public double? HeartRateBpm { get; set; }
    public double? PaceSecPerKm { get; set; }
    public double? CadenceSpm { get; set; }
    public double? SpeedKmh { get; set; }
    public double? StrideLengthCm { get; set; }
    public double? VerticalOscillationCm { get; set; }
    public double? VerticalRatioPercent { get; set; }
    public double? GroundContactTimeMs { get; set; }
}

/// <summary>Straight from PersonalRecords.csv — "fastest 5K" etc. without any recomputation.</summary>
public class PersonalRecord
{
    public int Id { get; set; }
    public long? WorkoutId { get; set; }
    public string NameLocalizationId { get; set; } = "";
    public string State { get; set; } = "";
    public DateTime AchieveTimeUtc { get; set; }
    public double RecordValue { get; set; }
    public string RecordType { get; set; } = "";
    public double? ExtentValueMeters { get; set; }
}
