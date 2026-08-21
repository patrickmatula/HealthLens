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

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();
