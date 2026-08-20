using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
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
/// Tracks which SQLite database is currently "live" — an in-memory ephemeral one, or the on-disk
/// persistent one — and hands out contexts and raw connections against it. The ephemeral database is
/// a named shared-cache one rather than a single shared <see cref="SqliteConnection"/> object, so a
/// request served while an import is running gets its own connection instead of racing the importer
/// on a connection that isn't thread-safe; one keep-alive connection holds the database in memory.
/// </summary>
public sealed class DataSessionService(IWebHostEnvironment env, ILogger<DataSessionService> logger)
{
    private readonly Lock _gate = new();
    private SqliteConnection? _ephemeralKeepAlive;
    private Session _session = Session.None;

    public DataMode Mode => Current.Mode;

    public bool HasData { get; private set; }

    public string PersistentDbPath => Path.Combine(env.ContentRootPath, "App_Data", "health.db");

    // WAL lets the API keep reading while an import writes; NORMAL is WAL's recommended durability
    // pairing (safe across app crashes, only an OS-level crash can lose the last commits).
    private const string PersistentPragmas = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=10000;";

    // Shared-cache in-memory databases take table-level locks, which busy_timeout does not wait out;
    // reading uncommitted rows keeps API requests from failing outright while an import holds a table.
    private const string EphemeralPragmas = "PRAGMA read_uncommitted=1; PRAGMA busy_timeout=10000;";

    // Only for the import's own write connection: an interrupted import is discarded and re-run anyway.
    private const string ImportPragmas = " PRAGMA synchronous=OFF; PRAGMA temp_store=MEMORY; PRAGMA cache_size=-65536;";

    /// <summary>Called once at startup. Attaches to the persistent DB file if one already exists, otherwise starts fresh in-memory.</summary>
    public async Task InitializeAsync()
    {
        if (File.Exists(PersistentDbPath))
        {
            logger.LogInformation("Found existing persistent database at {Path}", PersistentDbPath);
            await SwitchToPersistentAsync();

            await using var db = CreateContext();
            HasData = await db.ImportRuns.AnyAsync(r => r.Status == ImportStatus.Completed);
        }
        else
        {
            await SwitchToEphemeralAsync();
        }
    }

    public async Task SwitchToPersistentAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PersistentDbPath)!);
        Activate(new SqliteConnectionStringBuilder { DataSource = PersistentDbPath }.ToString(), DataMode.Persistent, PersistentPragmas);

        await using var db = CreateContext();
        ArchiveIfSchemaDrifted(db);
        await db.Database.EnsureCreatedAsync();
        StampSchemaVersion(db);
    }

    public async Task SwitchToEphemeralAsync()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = "ghl-" + Guid.NewGuid().ToString("N"),
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
        }.ToString();

        // The in-memory database lives exactly as long as one connection to it stays open.
        var keepAlive = new SqliteConnection(connectionString);
        keepAlive.Open();

        SqliteConnection? previous;
        lock (_gate)
        {
            previous = _ephemeralKeepAlive;
            _ephemeralKeepAlive = keepAlive;
        }

        previous?.Dispose();
        Activate(connectionString, DataMode.Ephemeral, EphemeralPragmas);

        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
        HasData = false;
    }

    public void MarkHasData() => HasData = true;

    public AppDbContext CreateContext() => new(Current.Options);

    /// <summary>An open connection to the live database, for the bulk import paths that bypass EF.</summary>
    public SqliteConnection OpenConnection(bool forBulkImport = false)
    {
        var session = Current;
        var connection = new SqliteConnection(session.ConnectionString);
        connection.Open();
        SqlitePragmaInterceptor.Apply(connection, forBulkImport ? session.Pragmas + ImportPragmas : session.Pragmas);
        return connection;
    }

    private Session Current
    {
        get
        {
            lock (_gate)
            {
                return _session;
            }
        }
    }

    private void Activate(string connectionString, DataMode mode, string pragmas)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connectionString)
            .AddInterceptors(new SqlitePragmaInterceptor(pragmas))
            .Options;

        lock (_gate)
        {
            _session = new Session(mode, connectionString, pragmas, options);
        }
    }

    /// <summary>
    /// The persistent database is created with EnsureCreated, which has no migration story: if the
    /// model has moved on since the file was written, EF would fail at the first query with a missing
    /// column. Stamping the model's shape into <c>PRAGMA user_version</c> lets us detect that and move
    /// the stale file aside — the user re-imports their Takeout zip, and the old file is still there.
    /// </summary>
    private void ArchiveIfSchemaDrifted(AppDbContext db)
    {
        if (!File.Exists(PersistentDbPath))
        {
            return;
        }

        int stored;
        using (var connection = OpenConnection())
        {
            stored = ReadSchemaVersion(connection);
        }

        var expected = SchemaVersion(db);
        if (stored == expected)
        {
            return;
        }

        SqliteConnection.ClearAllPools();
        var archived = $"{PersistentDbPath}.bak-{DateTime.Now:yyyyMMdd-HHmmss}";
        File.Move(PersistentDbPath, archived);
        foreach (var sidecar in new[] { "-wal", "-shm" })
        {
            if (File.Exists(PersistentDbPath + sidecar))
            {
                File.Move(PersistentDbPath + sidecar, archived + sidecar);
            }
        }

        logger.LogWarning(
            "Persistent database schema is out of date (was {Stored}, expected {Expected}); moved it to {Archived}. Re-import the Takeout export to repopulate.",
            stored,
            expected,
            archived);
    }

    private void StampSchemaVersion(AppDbContext db)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        // user_version takes a literal only; the value is a hash we computed, never user input.
        command.CommandText = $"PRAGMA user_version = {SchemaVersion(db)};";
        command.ExecuteNonQuery();
    }

    private static int ReadSchemaVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static int SchemaVersion(AppDbContext db)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(db.Database.GenerateCreateScript()));
        return BinaryPrimitives.ReadInt32LittleEndian(hash) & 0x7FFFFFFF;
    }

    private sealed record Session(DataMode Mode, string ConnectionString, string Pragmas, DbContextOptions<AppDbContext> Options)
    {
        public static readonly Session None = new(
            DataMode.Ephemeral,
            "Data Source=:memory:",
            "",
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options);
    }
}
