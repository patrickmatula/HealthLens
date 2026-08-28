namespace HealthLens.Api.Dtos;

public record ShoeDto(
    int Id,
    string Name,
    string? Brand,
    bool IsRetired,
    DateTime CreatedAtUtc,
    int WorkoutCount,
    double TotalDistanceMeters);

public record CreateShoeDto(string Name, string? Brand);

public record UpdateShoeDto(string Name, string? Brand, bool IsRetired);

// Workout ids are Fitbit's 64-bit exercise_id values, out of JS's safe-integer range — same
// string-id convention as WorkoutListItemDto/WorkoutDetailDto.
public record AssignShoeDto(int? ShoeId, IReadOnlyList<string> WorkoutIds);

public record ShoeDefaultDto(string Category, int? ShoeId);

public record SetShoeDefaultDto(int? ShoeId);
