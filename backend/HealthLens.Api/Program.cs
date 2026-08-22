using System.Text.Json.Serialization;
using HealthLens.Api.Data;
using HealthLens.Api.Services;
using HealthLens.Api.Services.GoogleHealth;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

builder.Services.AddSingleton<DataSessionService>();
builder.Services.AddSingleton<ImportJobRunner>();

builder.Services.AddSingleton<GoogleHealthCredentialStore>();
builder.Services.AddSingleton<GoogleHealthOAuthService>();
builder.Services.AddScoped<GoogleHealthSyncService>();
builder.Services.AddHttpClient<GoogleHealthApiClient>(c => c.BaseAddress = new Uri("https://health.googleapis.com/"));

// Takeout exports with years of intraday data can be large; raise the default multipart limits.
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 4L * 1024 * 1024 * 1024);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 4L * 1024 * 1024 * 1024);

// The container only: bind both the usual http port and an https one on a self-signed certificate
// generated on first run and persisted alongside the database. Nothing in the app requires https day
// to day — only the Google Health OAuth redirect does, and only that one popup ever touches this port
// (see GoogleHealthController.BuildCallbackUri). Local `dotnet run` keeps using launchSettings.json.
if (builder.Environment.IsProduction())
{
    var httpsCertPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "https-cert.pfx");
    var httpsCertificate = LocalHttpsCertificate.GetOrCreate(httpsCertPath);
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

app.MapControllers();
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html", staticFileOptions);

app.Run();
