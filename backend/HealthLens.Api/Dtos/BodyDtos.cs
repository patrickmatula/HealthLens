namespace HealthLens.Api.Dtos;

public record BodyProfileDto(double? HeightCm, string? Sex);

public record BodyMeasurementPointDto(DateOnly Date, string Type, double Value);

public record BodyOverviewDto(BodyProfileDto Profile, IReadOnlyList<BodyMeasurementPointDto> Measurements);

/// <summary>One day's worth of entries — only the types the user actually filled in are present.</summary>
public record BodyEntryDto(DateOnly Date, Dictionary<string, double> Values);
