namespace HealthLens.Api.Dtos;

public record GoogleHealthStatusDto(bool Configured, bool Connected, DateTime? LastSyncUtc, string? LastSyncSummary, string? LastError, string RedirectUri);

public record GoogleHealthConfigDto(string ClientId, string ClientSecret);

public record GoogleHealthAuthorizeDto(string Url);

public record GoogleHealthSyncResultDto(
    int ActivityDaysSynced,
    int RestingHeartRateDaysSynced,
    int WeightEntriesSynced,
    IReadOnlyList<string> Warnings);
