using System.Text.Json.Serialization;
using GoogleHealthLens.Api.Data;
using GoogleHealthLens.Api.Services;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

builder.Services.AddSingleton<DataSessionService>();
builder.Services.AddSingleton<ImportJobRunner>();
builder.Services.AddDbContext<AppDbContext>((sp, options) => sp.GetRequiredService<DataSessionService>().Configure(options));

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
