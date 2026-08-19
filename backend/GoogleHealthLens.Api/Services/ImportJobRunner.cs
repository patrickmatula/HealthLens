using System.Collections.Concurrent;
using System.IO.Compression;
using GoogleHealthLens.Api.Data;
using GoogleHealthLens.Api.Models;
using GoogleHealthLens.Api.Services.Importers;
using Microsoft.EntityFrameworkCore;

namespace GoogleHealthLens.Api.Services;

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
public class ImportJobRunner(DataSessionService session, ILogger<ImportJobRunner> logger)
{
    private static readonly IDomainImporter[] Importers =
    [
        new DailyActivityImporter(), new WorkoutsImporter(), new SleepImporter(), new HeartHealthImporter(), new RecoveryImporter(),
    ];

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
            ZipFile.ExtractToDirectory(zipPath, workDir);

            var googleHealthRoot = FindGoogleHealthFolder(workDir)
                ?? throw new InvalidOperationException("Im Zip wurde kein 'Google Health' Ordner gefunden. Bitte den originalen Google-Takeout-Export hochladen.");

            var context = new ImportContext(
                googleHealthRoot,
                scope,
                session.CreateContext,
                (step, pct) =>
                {
                    status.CurrentStep = step;
                    status.ProgressPercent = pct;
                });

            var perImporter = 100 / Importers.Length;
            for (var i = 0; i < Importers.Length; i++)
            {
                var importer = Importers[i];
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
}
