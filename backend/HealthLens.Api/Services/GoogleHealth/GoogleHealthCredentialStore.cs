using System.Text.Json;

namespace HealthLens.Api.Services.GoogleHealth;

/// <summary>
/// Everything the Google Health sync needs to remember: the user's own OAuth client (they register it
/// themselves in their own Google Cloud project — this app never ships a shared client secret), plus
/// the tokens obtained by authorizing. Deliberately a flat JSON file under App_Data rather than an EF
/// entity: it must never trigger the schema-drift archive-and-reimport cycle that any AppDbContext
/// model change causes, and it must survive independently of whether the Takeout database exists at all.
/// </summary>
public sealed class GoogleHealthCredentials
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTimeOffset? AccessTokenExpiresUtc { get; set; }
    public DateTime? LastSyncUtc { get; set; }
    public string? LastSyncSummary { get; set; }
    public string? LastError { get; set; }
}

public sealed class GoogleHealthCredentialStore(IWebHostEnvironment env)
{
    private readonly Lock _gate = new();

    private string FilePath => Path.Combine(env.ContentRootPath, "App_Data", "google-health.json");

    public GoogleHealthCredentials Load()
    {
        lock (_gate)
        {
            if (!File.Exists(FilePath))
            {
                return new GoogleHealthCredentials();
            }

            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<GoogleHealthCredentials>(json) ?? new GoogleHealthCredentials();
        }
    }

    public void Save(GoogleHealthCredentials credentials)
    {
        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(credentials, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
    }
}
