using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HealthLens.Api.Services.GoogleHealth;

/// <summary>
/// Standard Google OAuth 2.0 authorization-code flow with PKCE — this part is not Google-Health-API
/// specific and follows Google's documented Identity Platform contract exactly, unlike the data-sync
/// layer above it, which has to guess at an undocumented response shape.
/// </summary>
public sealed class GoogleHealthOAuthService(GoogleHealthCredentialStore store, IHttpClientFactory httpClientFactory, ILogger<GoogleHealthOAuthService> logger)
{
    public const string Scopes =
        "https://www.googleapis.com/auth/googlehealth.activity_and_fitness.readonly " +
        "https://www.googleapis.com/auth/googlehealth.health_metrics_and_measurements.readonly";

    private const string AuthorizeEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";

    // Short-lived link between the "state" round-tripped through Google's redirect and the PKCE
    // verifier we generated for that same attempt. In-memory is fine: a restart mid-authorization
    // just means the user clicks "Connect" again.
    private static readonly ConcurrentDictionary<string, (string Verifier, DateTimeOffset ExpiresUtc)> PendingAuthorizations = new();

    public string BuildAuthorizeUrl(string clientId, string redirectUri)
    {
        var verifier = RandomBase64Url(32);
        var challenge = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var state = RandomBase64Url(16);

        PendingAuthorizations[state] = (verifier, DateTimeOffset.UtcNow.AddMinutes(10));
        PruneExpired();

        var query = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = Scopes,
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["state"] = state,
        };

        return AuthorizeEndpoint + "?" + string.Join("&", query.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
    }

    /// <summary>Exchanges the authorization code from the callback for tokens and persists them.</summary>
    public async Task<bool> CompleteAuthorizationAsync(string code, string state, string redirectUri, CancellationToken ct)
    {
        if (!PendingAuthorizations.TryRemove(state, out var pending) || pending.ExpiresUtc < DateTimeOffset.UtcNow)
        {
            logger.LogWarning("Google Health OAuth callback with unknown or expired state.");
            return false;
        }

        var creds = store.Load();
        if (string.IsNullOrEmpty(creds.ClientId) || string.IsNullOrEmpty(creds.ClientSecret))
        {
            logger.LogWarning("Google Health OAuth callback arrived without a configured client.");
            return false;
        }

        using var client = httpClientFactory.CreateClient();
        using var response = await client.PostAsync(TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = creds.ClientId,
            ["client_secret"] = creds.ClientSecret,
            ["code_verifier"] = pending.Verifier,
        }), ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Google Health token exchange failed ({Status}): {Body}", response.StatusCode, body);
            creds.LastError = $"Token-Austausch fehlgeschlagen ({response.StatusCode}).";
            store.Save(creds);
            return false;
        }

        ApplyTokenResponse(creds, body);
        creds.LastError = null;
        store.Save(creds);
        return true;
    }

    /// <summary>Returns a valid access token, refreshing it first if it's missing or close to expiry.</summary>
    public async Task<string> EnsureFreshAccessTokenAsync(GoogleHealthCredentials creds, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(creds.RefreshToken))
        {
            throw new InvalidOperationException("Google Health ist nicht verbunden.");
        }

        if (!string.IsNullOrEmpty(creds.AccessToken) && creds.AccessTokenExpiresUtc is { } exp && exp > DateTimeOffset.UtcNow.AddMinutes(2))
        {
            return creds.AccessToken;
        }

        using var client = httpClientFactory.CreateClient();
        using var response = await client.PostAsync(TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = creds.RefreshToken,
            ["client_id"] = creds.ClientId ?? "",
            ["client_secret"] = creds.ClientSecret ?? "",
        }), ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            creds.LastError = $"Token-Erneuerung fehlgeschlagen ({response.StatusCode}). Bitte erneut verbinden.";
            store.Save(creds);
            throw new InvalidOperationException(creds.LastError);
        }

        ApplyTokenResponse(creds, body);
        store.Save(creds);
        return creds.AccessToken!;
    }

    private static void ApplyTokenResponse(GoogleHealthCredentials creds, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        creds.AccessToken = root.GetProperty("access_token").GetString();
        if (root.TryGetProperty("refresh_token", out var refreshProp))
        {
            // Google only returns a refresh_token on the very first consent (or when prompt=consent
            // forces re-consent); a bare refresh doesn't return one, so don't overwrite it with nothing.
            creds.RefreshToken = refreshProp.GetString();
        }

        var expiresIn = root.TryGetProperty("expires_in", out var expProp) ? expProp.GetInt32() : 3600;
        creds.AccessTokenExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
    }

    private static void PruneExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (key, value) in PendingAuthorizations)
        {
            if (value.ExpiresUtc < now)
            {
                PendingAuthorizations.TryRemove(key, out _);
            }
        }
    }

    private static string RandomBase64Url(int byteCount)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteCount);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
