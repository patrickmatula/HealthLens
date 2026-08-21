namespace HealthLens.Api.Dtos;

public record BodyProfileDto(double? HeightCm, string? Sex);

public record BodyMeasurementPointDto(DateOnly Date, string Type, string Side, double Value);

public record BodyOverviewDto(BodyProfileDto Profile, IReadOnlyList<BodyMeasurementPointDto> Measurements);

public record BodyEntryValueDto(string Type, string Side, double Value);

/// <summary>One day's worth of entries — only the types (and, for paired types, sides) the user actually filled in are present.</summary>
public record BodyEntryDto(DateOnly Date, IReadOnlyList<BodyEntryValueDto> Values);
