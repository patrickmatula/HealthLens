using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HealthLens.Api.Data;

/// <summary>
/// Applies the per-connection SQLite settings EF has no option for. Every connection needs them,
/// including the ones EF opens on its own, so they hang off the connection-opened hook rather than
/// being executed once at startup.
/// </summary>
public sealed class SqlitePragmaInterceptor(string pragmas) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
        Apply(connection, pragmas);

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Apply(connection, pragmas);
        return Task.CompletedTask;
    }

    public static void Apply(DbConnection connection, string pragmas)
    {
        using var command = connection.CreateCommand();
        command.CommandText = pragmas;
        command.ExecuteNonQuery();
    }
}
