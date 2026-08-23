using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

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

/// <summary>
/// The file holds an OAuth refresh token (durable access to the user's Google Health data), an access
/// token, and the user's own OAuth client secret -- encrypted at rest with ASP.NET Core's Data Protection
/// API rather than plain JSON, so a copy of just this file (a partial backup, a misdirected volume
/// mount) doesn't hand over a working credential on its own; decrypting it also requires the key ring
/// under App_Data/keys (see Program.cs).
/// </summary>
public sealed class GoogleHealthCredentialStore(IWebHostEnvironment env, IDataProtectionProvider dataProtectionProvider)
{
    private readonly Lock _gate = new();
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("HealthLens.GoogleHealthCredentials");

    private string FilePath => Path.Combine(env.ContentRootPath, "App_Data", "google-health.json");

    public GoogleHealthCredentials Load()
    {
        lock (_gate)
        {
            if (!File.Exists(FilePath))
            {
                return new GoogleHealthCredentials();
            }

            var raw = File.ReadAllText(FilePath);
            string json;
            try
            {
                json = _protector.Unprotect(raw);
            }
            catch (CryptographicException)
            {
                // Plaintext file from before this file was encrypted -- read it as-is once; the next
                // Save() (which happens on every connect/sync/config change) re-persists it encrypted.
                json = raw;
            }

            return JsonSerializer.Deserialize<GoogleHealthCredentials>(json) ?? new GoogleHealthCredentials();
        }
    }

    public void Save(GoogleHealthCredentials credentials)
    {
        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var json = JsonSerializer.Serialize(credentials, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, _protector.Protect(json));
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
