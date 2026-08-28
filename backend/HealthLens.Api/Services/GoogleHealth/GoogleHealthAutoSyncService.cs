using HealthLens.Api.Data;

namespace HealthLens.Api.Services.GoogleHealth;

/// <summary>
/// Optional "syncs itself once a day" background loop, opt-in via the auto-sync toggle in the app's
/// Google Health settings. There's no external scheduler in a single-process self-hosted app, so this
/// runs its own timer and simply checks every 15 minutes whether 24h have passed since the last
/// successful sync — more than precise enough for a "keeps the dashboard fresh" convenience feature, and
/// simple enough to survive container restarts without needing to persist a "next run" timestamp: it just
/// re-evaluates LastSyncUtc from the credential store on every tick.
/// </summary>
public sealed class GoogleHealthAutoSyncService(
    IServiceScopeFactory scopeFactory,
    ILogger<GoogleHealthAutoSyncService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan SyncInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);
        do
        {
            try
            {
                await RunIfDueAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Google Health auto-sync check failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunIfDueAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<GoogleHealthCredentialStore>();
        var session = scope.ServiceProvider.GetRequiredService<DataSessionService>();

        var creds = store.Load();
        if (!creds.AutoSyncEnabled || string.IsNullOrEmpty(creds.RefreshToken) || session.Mode != DataMode.Persistent)
        {
            return;
        }

        if (creds.LastSyncUtc is { } last && DateTime.UtcNow - last < SyncInterval)
        {
            return;
        }

        logger.LogInformation("Google Health auto-sync: starting scheduled daily sync");
        var sync = scope.ServiceProvider.GetRequiredService<GoogleHealthSyncService>();
        var result = await sync.SyncAsync(ct);
        logger.LogInformation(
            "Google Health auto-sync: completed ({WorkoutsSynced} workouts, {ActivityDays} activity days, {WeightEntries} weight entries)",
            result.WorkoutsSynced, result.ActivityDaysSynced, result.WeightEntriesSynced);
    }
}
