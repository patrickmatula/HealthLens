using HealthLens.Api.Data;
using HealthLens.Api.Dtos;
using HealthLens.Api.Services.GoogleHealth;
using Microsoft.AspNetCore.Mvc;

namespace HealthLens.Api.Controllers;

/// <summary>
/// Optional, minimal "keep the dashboard fresh between Takeout exports" sync against the Google Health
/// API. The user brings their own OAuth client (a free personal Google Cloud project) — this app never
/// ships or proxies a shared client secret, matching its "your data never leaves this machine except to
/// the API you explicitly authorized" stance.
/// </summary>
[ApiController]
[Route("api/googlehealth")]
public class GoogleHealthController(
    GoogleHealthCredentialStore store,
    GoogleHealthOAuthService oauth,
    GoogleHealthSyncService sync,
    DataSessionService session,
    IWebHostEnvironment env) : ControllerBase
{
    [HttpGet("status")]
    public ActionResult<GoogleHealthStatusDto> Status()
    {
        var creds = store.Load();
        var configured = !string.IsNullOrEmpty(creds.ClientId) && !string.IsNullOrEmpty(creds.ClientSecret);
        var connected = configured && !string.IsNullOrEmpty(creds.RefreshToken);
        return Ok(new GoogleHealthStatusDto(configured, connected, creds.LastSyncUtc, creds.LastSyncSummary, creds.LastError, BuildCallbackUri()));
    }

    [HttpPost("config")]
    public ActionResult<GoogleHealthStatusDto> SaveConfig(GoogleHealthConfigDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ClientId) || string.IsNullOrWhiteSpace(dto.ClientSecret))
        {
            return BadRequest("Client-ID und Client-Secret dürfen nicht leer sein.");
        }

        var creds = store.Load();
        creds.ClientId = dto.ClientId.Trim();
        creds.ClientSecret = dto.ClientSecret.Trim();
        creds.LastError = null;
        store.Save(creds);

        return Status();
    }

    [HttpDelete("config")]
    public IActionResult Disconnect()
    {
        store.Clear();
        return NoContent();
    }

    [HttpGet("authorize")]
    public ActionResult<GoogleHealthAuthorizeDto> Authorize()
    {
        var creds = store.Load();
        if (string.IsNullOrEmpty(creds.ClientId))
        {
            return BadRequest("Bitte zuerst Client-ID und Client-Secret speichern.");
        }

        var redirectUri = BuildCallbackUri();
        var url = oauth.BuildAuthorizeUrl(creds.ClientId, redirectUri);
        return Ok(new GoogleHealthAuthorizeDto(url));
    }

    /// <summary>The OAuth redirect target — must match the redirect URI registered in Google Cloud exactly.</summary>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            return Content(CallbackHtml("Verbindung abgebrochen oder fehlgeschlagen. Du kannst dieses Fenster schließen und es erneut versuchen."), "text/html");
        }

        var success = await oauth.CompleteAuthorizationAsync(code, state, BuildCallbackUri(), ct);
        return Content(
            CallbackHtml(success
                ? "Verbunden! Du kannst dieses Fenster schließen."
                : "Verbindung fehlgeschlagen. Du kannst dieses Fenster schließen und es erneut versuchen."),
            "text/html");
    }

    [HttpPost("sync")]
    public async Task<ActionResult<GoogleHealthSyncResultDto>> Sync(CancellationToken ct)
    {
        if (session.Mode != DataMode.Persistent)
        {
            return BadRequest("Synchronisierung ist nur im dauerhaften Speichermodus verfügbar.");
        }

        try
        {
            var result = await sync.SyncAsync(ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // Google requires https for the redirect URI. The container listens on 8443 for exactly this
    // (see Program.cs) with a self-signed cert generated on first run, so this is forced to https+8443
    // regardless of which port the current page happens to be loaded from — only the OAuth popup ever
    // touches it, so the rest of the app keeps using plain http day to day.
    private string BuildCallbackUri() =>
        env.IsProduction()
            ? $"https://{Request.Host.Host}:8443/api/googlehealth/callback"
            : $"{Request.Scheme}://{Request.Host}/api/googlehealth/callback";

    private static string CallbackHtml(string message) =>
        $"""
         <!doctype html>
         <html><body style="font-family: system-ui, sans-serif; padding: 2rem; text-align: center;">
         <p>{System.Net.WebUtility.HtmlEncode(message)}</p>
         <script>setTimeout(() => window.close(), 2000);</script>
         </body></html>
         """;
}
