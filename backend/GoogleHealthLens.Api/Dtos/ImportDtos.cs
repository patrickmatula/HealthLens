using GoogleHealthLens.Api.Models;

namespace GoogleHealthLens.Api.Dtos;

public record ImportStartedDto(int JobId);

public record ImportStatusDto(int Id, string Status, string CurrentStep, int ProgressPercent, long RowsImported, string? ErrorMessage);

public record ImportCurrentDto(bool HasData, string Mode, DateTime? LastImportAtUtc, ImportScope? LastScope, long? RowsImported);
