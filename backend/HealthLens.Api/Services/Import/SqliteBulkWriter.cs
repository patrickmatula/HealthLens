using Microsoft.Data.Sqlite;

namespace HealthLens.Api.Services.Import;

/// <summary>
/// Prepared-statement, transaction-batched writer for the high-volume import tables. EF change
/// tracking costs more than the insert itself at these row counts, so the bulk paths bypass it and
/// go straight at the connection. Calls are synchronous on purpose: the SQLite provider is
/// synchronous underneath and the async wrappers only add a state machine per row.
/// </summary>
public sealed class SqliteBulkWriter : IDisposable
{
    private const int RowsPerTransaction = 100_000;

    private readonly SqliteConnection _connection;
    private readonly SqliteCommand _command;
    private SqliteTransaction? _transaction;
    private int _pendingRows;

    public SqliteBulkWriter(SqliteConnection connection, string sql, params ReadOnlySpan<string> parameterNames)
    {
        _connection = connection;
        _command = connection.CreateCommand();
        _command.CommandText = sql;

        foreach (var name in parameterNames)
        {
            var parameter = _command.CreateParameter();
            parameter.ParameterName = name;
            _command.Parameters.Add(parameter);
        }
    }

    /// <summary>Rows handed to <see cref="Write"/>; SQLite may still have ignored some as duplicates.</summary>
    public long RowCount { get; private set; }

    public void Write(params ReadOnlySpan<object?> values)
    {
        if (_transaction is null)
        {
            _transaction = _connection.BeginTransaction();
            _command.Transaction = _transaction;
        }

        for (var i = 0; i < values.Length; i++)
        {
            _command.Parameters[i].Value = values[i] ?? DBNull.Value;
        }

        _command.ExecuteNonQuery();
        RowCount++;

        if (++_pendingRows >= RowsPerTransaction)
        {
            Flush();
        }
    }

    public void Flush()
    {
        if (_transaction is null)
        {
            return;
        }

        _transaction.Commit();
        _transaction.Dispose();
        _transaction = null;
        _command.Transaction = null;
        _pendingRows = 0;
    }

    public void Dispose()
    {
        Flush();
        _command.Dispose();
    }
}
