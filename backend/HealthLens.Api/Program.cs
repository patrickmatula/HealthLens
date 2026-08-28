using System.Text.Json.Serialization;
using HealthLens.Api.Data;
using HealthLens.Api.Services;
using HealthLens.Api.Services.GoogleHealth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

builder.Services.AddSingleton<DataSessionService>();
builder.Services.AddSingleton<ImportJobRunner>();

// Persisted to App_Data (not the default OS-specific profile path) so the key ring survives container
// recreation on the same volume — required for anything protected with it (OAuth tokens below, the
// HTTPS private key) to still decrypt after a redeploy, and keeps everything sensitive under the one
// directory that's already the app's single persistence boundary.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")));

builder.Services.AddSingleton<GoogleHealthCredentialStore>();
builder.Services.AddSingleton<GoogleHealthOAuthService>();
builder.Services.AddScoped<GoogleHealthSyncService>();
builder.Services.AddHttpClient<GoogleHealthApiClient>(c => c.BaseAddress = new Uri("https://health.googleapis.com/"));
builder.Services.AddHostedService<GoogleHealthAutoSyncService>();

// Takeout exports with years of intraday data can be large; raise the default multipart limits.
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 4L * 1024 * 1024 * 1024);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 4L * 1024 * 1024 * 1024);

// The container only: bind both the usual http port and an https one on a self-signed certificate
// generated on first run and persisted alongside the database. Nothing in the app requires https day
// to day — only the Google Health OAuth redirect does, and only that one popup ever touches this port
// (see GoogleHealthController.BuildCallbackUri). Local `dotnet run` keeps using launchSettings.json.
if (builder.Environment.IsProduction())
{
    // Standalone provider pointed at the same key-ring directory registered above -- this runs before
    // builder.Build() produces a DI container to pull the registered IDataProtectionProvider from.
    var keysDirectory = new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys"));
    var certProtector = DataProtectionProvider.Create(keysDirectory).CreateProtector("HealthLens.HttpsCert");

    var httpsCertPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "https-cert.pfx");
    var httpsCertificate = LocalHttpsCertificate.GetOrCreate(httpsCertPath, certProtector);
    builder.WebHost.ConfigureKestrel(o =>
    {
        o.ListenAnyIP(8080);
        o.ListenAnyIP(8443, lo => lo.UseHttps(httpsCertificate));
    });
}

const string DevCorsPolicy = "DevCors";
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options => options.AddPolicy(DevCorsPolicy, policy =>
        policy.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var session = scope.ServiceProvider.GetRequiredService<DataSessionService>();
    await session.InitializeAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors(DevCorsPolicy);
}

// Vite content-hashes every file under /assets/, so those can be cached "forever" — a changed file is
// a new URL. index.html (and anything else outside /assets/) has a stable name and must always be
// revalidated, or a browser can sit on a stale SPA shell pointing at deleted hashed asset files
// indefinitely, silently missing every subsequent deploy until a hard refresh.
var staticFileOptions = new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.CacheControl = ctx.Context.Request.Path.StartsWithSegments("/assets")
            ? "public, max-age=31536000, immutable"
            : "no-cache";
    },
};

app.UseDefaultFiles();
app.UseStaticFiles(staticFileOptions);

app.UseAuthorization();

// HealthLens has no login by design (self-hosted, meant for your own trusted network only — see
// README's "Security" section). That still leaves state-changing endpoints open to being blindly
// triggered by a malicious page a browser on that network happens to visit — a classic CSRF pattern,
// just without a session to steal. A plain cross-site <form> submit can't set custom headers, so
// requiring this one on every mutating /api request forces the browser into a CORS preflight; since no
// CORS policy grants cross-origin access in production, that preflight fails and the browser blocks the
// real request before it reaches here. GET requests are intentionally exempt: they're already
// unreadable cross-origin under the same-origin policy, and the one GET that itself changes state (the
// OAuth callback) is separately protected by its own single-use, expiring `state` parameter.
const string AntiCsrfHeader = "X-HealthLens-Client";
app.Use(async (context, next) =>
{
    var method = context.Request.Method;
    var isMutating = HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsDelete(method) || HttpMethods.IsPatch(method);
    if (context.Request.Path.StartsWithSegments("/api") && isMutating && !context.Request.Headers.ContainsKey(AntiCsrfHeader))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsync("Missing required header.");
        return;
    }

    await next(context);
});

app.MapControllers();
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html", staticFileOptions);

app.Run();
