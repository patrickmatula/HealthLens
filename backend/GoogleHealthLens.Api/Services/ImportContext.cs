using GoogleHealthLens.Api.Data;
using GoogleHealthLens.Api.Models;

namespace GoogleHealthLens.Api.Services;

/// <summary>Everything a domain importer needs: where the extracted "Google Health" folder is, which scope was requested, and how to report progress.</summary>
public class ImportContext(string googleHealthRoot, ImportScope scope, Func<AppDbContext> createContext, Action<string, int> reportProgress)
{
    public string GoogleHealthRoot { get; } = googleHealthRoot;
    public ImportScope Scope { get; } = scope;
    public Func<AppDbContext> CreateContext { get; } = createContext;

    /// <summary>step description, percent 0-100 for this importer's own slice of the overall progress bar</summary>
    public Action<string, int> ReportProgress { get; } = reportProgress;

    public long RowsImported { get; set; }

    public string? FindFolder(string name)
    {
        var direct = Path.Combine(GoogleHealthRoot, name);
        return Directory.Exists(direct) ? direct : null;
    }
}

public interface IDomainImporter
{
    string Name { get; }
    Task ImportAsync(ImportContext context, CancellationToken ct);
}
