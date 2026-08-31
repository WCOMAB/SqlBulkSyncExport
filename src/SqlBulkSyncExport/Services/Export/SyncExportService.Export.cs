using System.Data;
using Microsoft.Data.SqlClient;
using SqlBulkSyncExport.Helpers;

namespace SqlBulkSyncExport.Services.Export;

public sealed partial class SyncExportService
{
    private async Task<long> ExportFullAsync(
        SqlConnection connection,
        ResolvedTableExport table,
        IReadOnlyList<Column> columns,
        int commandTimeoutSeconds,
        string path,
        CsvWriteOptions csvOptions,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = SqlStatements.GetSelectAllStatement(
            table.SourceTableName,
            columns,
            table.UseSnapshotIsolation);
        command.CommandTimeout = commandTimeoutSeconds;

        if (table.UseSnapshotIsolation)
        {
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
                IsolationLevel.Snapshot,
                cancellationToken);
            command.Transaction = transaction;
            await using var reader = await command.ExecuteReaderAsync(
                CommandBehavior.SequentialAccess,
                cancellationToken);
            var rows = await csvExportWriter.WriteAsync(path, reader, csvOptions, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return rows;
        }

        await using (var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken))
        {
            return await csvExportWriter.WriteAsync(path, reader, csvOptions, cancellationToken);
        }
    }

    private async Task<long> ExportDeltaAsync(
        SqlConnection connection,
        string sourceTableName,
        IReadOnlyList<Column> columns,
        long fromVersion,
        int commandTimeoutSeconds,
        string path,
        CsvWriteOptions csvOptions,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = SqlStatements.GetNewOrUpdatedSelectStatement(sourceTableName, columns);
        command.CommandTimeout = commandTimeoutSeconds;
        command.Parameters.Add("@FromVersion", SqlDbType.BigInt).Value = fromVersion;

        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken);
        return await csvExportWriter.WriteAsync(path, reader, csvOptions, cancellationToken);
    }

    private async Task<long> ExportDeletedAsync(
        SqlConnection connection,
        string sourceTableName,
        IReadOnlyList<Column> columns,
        long fromVersion,
        int commandTimeoutSeconds,
        string path,
        CsvWriteOptions csvOptions,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = SqlStatements.GetDeletedPrimaryKeysSelectStatement(sourceTableName, columns);
        command.CommandTimeout = commandTimeoutSeconds;
        command.Parameters.Add("@FromVersion", SqlDbType.BigInt).Value = fromVersion;

        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken);
        return await csvExportWriter.WriteAsync(path, reader, csvOptions, cancellationToken);
    }
}
