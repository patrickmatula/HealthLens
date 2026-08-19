namespace GoogleHealthLens.Api.Models;

public enum ImportStatus
{
    Running,
    Completed,
    Failed,
}

public enum ImportScope
{
    Curated,
    Full,
}

/// <summary>Records one import job. In persistent mode, rows accumulate across re-imports; in ephemeral mode there's ever only one.</summary>
public class ImportRun
{
    public int Id { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public ImportStatus Status { get; set; }
    public ImportScope Scope { get; set; }
    public string SourceZipName { get; set; } = "";
    public string CurrentStep { get; set; } = "";
    public int ProgressPercent { get; set; }
    public long RowsImported { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? EarliestDataUtc { get; set; }
    public DateTime? LatestDataUtc { get; set; }
}
