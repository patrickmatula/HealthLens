using HealthLens.Api.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HealthLens.Api.Data;

public enum DataMode
{
    Ephemeral,
    Persistent,
}

/// <summary>
/// Tracks which SQLite database is currently "live" (an in-memory ephemeral one, or the on-disk
/// persistent one) and configures freshly-created <see cref="AppDbContext"/> instances against it.
/// Switching modes takes effect immediately for the next DbContext created, since AddDbContext's
/// options-configure delegate re-runs on every scope.
/// </summary>
public class DataSessionService(IWebHostEnvironment env, ILogger<DataSessionService> logger)
{
    private readonly object _lock = new();
    private SqliteConnection? _ephemeralConnection;

    public DataMode Mode { get; private set; } = DataMode.Ephemeral;
    public bool HasData { get; private set; }

    public string PersistentDbPath => Path.Combine(env.ContentRootPath, "App_Data", "health.db");

    public void Configure(DbContextOptionsBuilder builder)
    {
        lock (_lock)
        {
            if (Mode == DataMode.Persistent)
            {
                builder.UseSqlite($"Data Source={PersistentDbPath}");
            }
            else
            {
                EnsureEphemeralConnectionLocked();
                builder.UseSqlite(_ephemeralConnection!);
            }
        }
    }

    private void EnsureEphemeralConnectionLocked()
    {
        if (_ephemeralConnection is not null)
        {
            return;
        }

        _ephemeralConnection = new SqliteConnection("Data Source=:memory:");
        _ephemeralConnection.Open();
    }

    /// <summary>Called once at startup. Attaches to the persistent DB file if one already exists, otherwise starts fresh in-memory.</summary>
    public async Task InitializeAsync()
    {
        if (File.Exists(PersistentDbPath))
        {
            logger.LogInformation("Found existing persistent database at {Path}", PersistentDbPath);
            lock (_lock)
            {
                Mode = DataMode.Persistent;
            }

            using var context = CreateContext();
            await context.Database.EnsureCreatedAsync();
            HasData = await context.ImportRuns.AnyAsync(r => r.Status == ImportStatus.Completed);
        }
        else
        {
            lock (_lock)
            {
                Mode = DataMode.Ephemeral;
                EnsureEphemeralConnectionLocked();
            }

            using var context = CreateContext();
            await context.Database.EnsureCreatedAsync();
            HasData = false;
        }
    }

    public async Task SwitchToPersistentAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PersistentDbPath)!);
        lock (_lock)
        {
            Mode = DataMode.Persistent;
        }

        using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
    }

    public async Task SwitchToEphemeralAsync()
    {
        SqliteConnection? old;
        lock (_lock)
        {
            old = _ephemeralConnection;
            _ephemeralConnection = null;
            Mode = DataMode.Ephemeral;
            EnsureEphemeralConnectionLocked();
        }

        old?.Dispose();

        using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
        HasData = false;
    }

    public void MarkHasData() => HasData = true;

    public AppDbContext CreateContext()
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        Configure(builder);
        return new AppDbContext(builder.Options);
    }
}
