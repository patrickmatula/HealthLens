using System.Collections.Concurrent;
using System.IO.Compression;
using HealthLens.Api.Data;
using HealthLens.Api.Models;
using HealthLens.Api.Services.Importers;
using Microsoft.EntityFrameworkCore;

namespace HealthLens.Api.Services;

public class ImportJobStatus
{
    public int Id { get; set; }
    public ImportStatus Status { get; set; }
    public string CurrentStep { get; set; } = "";
    public int ProgressPercent { get; set; }
    public long RowsImported { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Runs a single import at a time in the background: extracts the uploaded zip, switches the
/// DataSessionService to the requested mode, and dispatches every registered <see cref="IDomainImporter"/>
/// over the extracted "Google Health" folder while reporting progress that the frontend polls for.
/// </summary>
public class ImportJobRunner(DataSessionService session, ILogger<ImportJobRunner> logger, ShoeDefaultsStore shoeDefaults)
{
    private readonly IDomainImporter[] _importers =
    [
        new DailyActivityImporter(), new WorkoutsImporter(shoeDefaults), new SleepImporter(), new HeartHealthImporter(), new RecoveryImporter(),
    ];

    // Zip-bomb guard: a small, deeply-compressed zip can otherwise claim to expand to an arbitrary size
    // on disk. Both caps are generous for even a "Full" scope, multi-year Takeout export (which tops out
    // around a few GB decompressed and a few thousand per-day files in real exports) while still catching
    // a deliberately malicious archive long before it can fill the disk.
    private const long MaxUncompressedBytes = 50L * 1024 * 1024 * 1024;
    private const int MaxEntries = 500_000;

    private readonly ConcurrentDictionary<int, ImportJobStatus> _jobs = new();
    private int _nextId;
    private int _running;

    public int Start(string zipPath, string originalFileName, bool persistent, ImportScope scope)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            throw new InvalidOperationException("Es läuft bereits ein Import.");
        }

        var id = Interlocked.Increment(ref _nextId);
        var status = new ImportJobStatus { Id = id, Status = ImportStatus.Running, CurrentStep = "Import wird gestartet..." };
        _jobs[id] = status;

        _ = Task.Run(() => RunAsync(id, zipPath, originalFileName, persistent, scope));

        return id;
    }

    public ImportJobStatus? GetStatus(int id) => _jobs.GetValueOrDefault(id);

    private async Task RunAsync(int id, string zipPath, string originalFileName, bool persistent, ImportScope scope)
    {
        var status = _jobs[id];
        string? workDir = null;
        try
        {
            if (persistent)
            {
                await session.SwitchToPersistentAsync();
            }
            else
            {
                await session.SwitchToEphemeralAsync();
            }

            var importRun = new ImportRun
            {
                StartedAtUtc = DateTime.UtcNow,
                Status = ImportStatus.Running,
                Scope = scope,
                SourceZipName = originalFileName,
            };

            await using (var db = session.CreateContext())
            {
                db.ImportRuns.Add(importRun);
                await db.SaveChangesAsync();
            }

            status.CurrentStep = "Zip wird entpackt...";
            workDir = Path.Combine(Path.GetTempPath(), "ghl-import-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workDir);
            ExtractZipSafely(zipPath, workDir);

            var googleHealthRoot = FindGoogleHealthFolder(workDir)
                ?? throw new InvalidOperationException("Im Zip wurde kein 'Google Health' Ordner gefunden. Bitte den originalen Google-Takeout-Export hochladen.");

            var context = new ImportContext(
                googleHealthRoot,
                scope,
                session,
                (step, pct) =>
                {
                    status.CurrentStep = step;
                    status.ProgressPercent = pct;
                });

            var perImporter = 100 / _importers.Length;
            for (var i = 0; i < _importers.Length; i++)
            {
                var importer = _importers[i];
                status.CurrentStep = $"{importer.Name}...";
                await importer.ImportAsync(context, CancellationToken.None);
                status.ProgressPercent = (i + 1) * perImporter;
            }

            await using (var db = session.CreateContext())
            {
                var run = await db.ImportRuns.OrderByDescending(r => r.Id).FirstAsync();
                run.Status = ImportStatus.Completed;
                run.CompletedAtUtc = DateTime.UtcNow;
                run.RowsImported = context.RowsImported;
                await db.SaveChangesAsync();
            }

            session.MarkHasData();
            status.Status = ImportStatus.Completed;
            status.ProgressPercent = 100;
            status.RowsImported = context.RowsImported;
            status.CurrentStep = "Import abgeschlossen";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Import {Id} fehlgeschlagen", id);
            status.Status = ImportStatus.Failed;
            status.ErrorMessage = ex.Message;
        }
        finally
        {
            if (workDir is not null && Directory.Exists(workDir))
            {
                try { Directory.Delete(workDir, recursive: true); } catch { /* best effort cleanup */ }
            }

            try { File.Delete(zipPath); } catch { /* best effort cleanup */ }
            Volatile.Write(ref _running, 0);
        }
    }

    private static string? FindGoogleHealthFolder(string root)
    {
        if (Path.GetFileName(root) == "Google Health")
        {
            return root;
        }

        return Directory.EnumerateDirectories(root, "Google Health", SearchOption.AllDirectories).FirstOrDefault();
    }

    /// <summary>
    /// Extracts entry-by-entry instead of the one-shot ZipFile.ExtractToDirectory, to enforce the
    /// zip-bomb caps above (checked against each entry's declared uncompressed length before writing it,
    /// so a bomb is caught before its oversized entry is written rather than after) while still rejecting
    /// path traversal exactly like the convenience method does: resolve the destination path and require
    /// it stay under destinationRoot.
    /// </summary>
    private static void ExtractZipSafely(string zipPath, string destinationDir)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        if (archive.Entries.Count > MaxEntries)
        {
            throw new InvalidOperationException($"Zip enthält zu viele Dateien ({archive.Entries.Count}).");
        }

        var destinationRoot = Path.GetFullPath(destinationDir + Path.DirectorySeparatorChar);
        long totalUncompressedBytes = 0;

        foreach (var entry in archive.Entries)
        {
            totalUncompressedBytes += entry.Length;
            if (totalUncompressedBytes > MaxUncompressedBytes)
            {
                throw new InvalidOperationException("Zip enthält zu viele entpackte Daten (möglicherweise eine Zip-Bomb).");
            }

            var destinationPath = Path.GetFullPath(Path.Combine(destinationDir, entry.FullName));
            if (!destinationPath.StartsWith(destinationRoot, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Ungültiger Zip-Eintrag außerhalb des Zielverzeichnisses.");
            }

            if (entry.Name.Length == 0)
            {
                // Directory entry.
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            entry.ExtractToFile(destinationPath, overwrite: true);
        }
    }
}
