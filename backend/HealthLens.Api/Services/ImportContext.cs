using HealthLens.Api.Data;
using HealthLens.Api.Models;
using Microsoft.Data.Sqlite;

namespace HealthLens.Api.Services;

/// <summary>Everything a domain importer needs: where the extracted "Google Health" folder is, which scope was requested, how to reach the live database, and how to report progress.</summary>
public sealed class ImportContext(string googleHealthRoot, ImportScope scope, DataSessionService session, Action<string, int> reportProgress)
{
    private long _rowsImported;

    public string GoogleHealthRoot { get; } = googleHealthRoot;

    public ImportScope Scope { get; } = scope;

    /// <summary>step description, percent 0-100 for this importer's own slice of the overall progress bar</summary>
    public Action<string, int> ReportProgress { get; } = reportProgress;

    public long RowsImported => Interlocked.Read(ref _rowsImported);

    /// <summary>Importers parse in parallel, so the running total has to be interlocked.</summary>
    public void AddRows(long count) => Interlocked.Add(ref _rowsImported, count);

    public AppDbContext CreateContext() => session.CreateContext();

    /// <summary>An open, import-tuned connection for the bulk paths that bypass EF change tracking.</summary>
    public SqliteConnection OpenWriteConnection() => session.OpenConnection(forBulkImport: true);

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
