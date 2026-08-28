namespace HealthLens.Api.Dtos;

public record GoogleHealthStatusDto(bool Configured, bool Connected, DateTime? LastSyncUtc, string? LastSyncSummary, string? LastError, string RedirectUri, bool AutoSyncEnabled);

public record GoogleHealthConfigDto(string ClientId, string ClientSecret);

public record GoogleHealthAutoSyncDto(bool Enabled);

public record GoogleHealthAuthorizeDto(string Url);

public record GoogleHealthSyncResultDto(
    int ActivityDaysSynced,
    int RestingHeartRateDaysSynced,
    int WeightEntriesSynced,
    int WorkoutsSynced,
    IReadOnlyList<string> Warnings);
