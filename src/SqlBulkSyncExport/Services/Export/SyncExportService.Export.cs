using System.Data;
using System.Globalization;
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
        CsvWriteOptions csvOptionsBase,
        int? progressLogBatchSize,
        CancellationToken cancellationToken)
    {
        if (table.UseSnapshotIsolation)
        {
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
                IsolationLevel.Snapshot,
                cancellationToken);

            var csvOptions = await ResolveProgressOptionsAsync(
                connection,
                transaction,
                SqlStatements.GetSelectAllCountStatement(table.SourceTableName, useSnapshotIsolation: true),
                fromVersion: null,
                commandTimeoutSeconds,
                csvOptionsBase,
                progressLogBatchSize,
                cancellationToken);

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = SqlStatements.GetSelectAllStatement(
                table.SourceTableName,
                columns,
                useSnapshotIsolation: true);
            command.CommandTimeout = commandTimeoutSeconds;

            await using var reader = await command.ExecuteReaderAsync(
                CommandBehavior.SequentialAccess,
                cancellationToken);
            var rows = await csvExportWriter.WriteAsync(path, reader, csvOptions, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return rows;
        }

        {
            var csvOptions = await ResolveProgressOptionsAsync(
                connection,
                transaction: null,
                SqlStatements.GetSelectAllCountStatement(table.SourceTableName, useSnapshotIsolation: false),
                fromVersion: null,
                commandTimeoutSeconds,
                csvOptionsBase,
                progressLogBatchSize,
                cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = SqlStatements.GetSelectAllStatement(
                table.SourceTableName,
                columns,
                useSnapshotIsolation: false);
            command.CommandTimeout = commandTimeoutSeconds;

            await using var reader = await command.ExecuteReaderAsync(
                CommandBehavior.SequentialAccess,
                cancellationToken);
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
        CsvWriteOptions csvOptionsBase,
        int? progressLogBatchSize,
        CancellationToken cancellationToken)
    {
        var csvOptions = await ResolveProgressOptionsAsync(
            connection,
            transaction: null,
            SqlStatements.GetNewOrUpdatedCountStatement(sourceTableName, columns),
            fromVersion,
            commandTimeoutSeconds,
            csvOptionsBase,
            progressLogBatchSize,
            cancellationToken);

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
        CsvWriteOptions csvOptionsBase,
        int? progressLogBatchSize,
        CancellationToken cancellationToken)
    {
        var csvOptions = await ResolveProgressOptionsAsync(
            connection,
            transaction: null,
            SqlStatements.GetDeletedPrimaryKeysCountStatement(sourceTableName, columns),
            fromVersion,
            commandTimeoutSeconds,
            csvOptionsBase,
            progressLogBatchSize,
            cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = SqlStatements.GetDeletedPrimaryKeysSelectStatement(sourceTableName, columns);
        command.CommandTimeout = commandTimeoutSeconds;
        command.Parameters.Add("@FromVersion", SqlDbType.BigInt).Value = fromVersion;

        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess,
            cancellationToken);
        return await csvExportWriter.WriteAsync(path, reader, csvOptions, cancellationToken);
    }

    private async Task<CsvWriteOptions> ResolveProgressOptionsAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        string countCommandText,
        long? fromVersion,
        int commandTimeoutSeconds,
        CsvWriteOptions csvOptionsBase,
        int? progressLogBatchSize,
        CancellationToken cancellationToken)
    {
        long? countedRows = null;
        if (progressLogBatchSize is null)
        {
            try
            {
                countedRows = await ExecuteCountAsync(
                    connection,
                    transaction,
                    countCommandText,
                    fromVersion,
                    commandTimeoutSeconds,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to COUNT export rows for progress logging; falling back to batch size {BatchSize}.",
                    ProgressLogBatchResolver.FallbackBatchSize);
            }
        }

        var (batchSize, estimatedTotalRows) = ProgressLogBatchResolver.Resolve(progressLogBatchSize, countedRows);
        return csvOptionsBase with
        {
            ProgressLogBatchSize = batchSize,
            EstimatedTotalRows = estimatedTotalRows
        };
    }

    private static async Task<long> ExecuteCountAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        string commandText,
        long? fromVersion,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        command.CommandTimeout = commandTimeoutSeconds;
        if (fromVersion is not null)
        {
            command.Parameters.Add("@FromVersion", SqlDbType.BigInt).Value = fromVersion.Value;
        }

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? 0L : Convert.ToInt64(result, CultureInfo.InvariantCulture);
    }
}
