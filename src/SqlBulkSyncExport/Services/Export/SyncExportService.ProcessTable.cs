using Microsoft.Data.SqlClient;

namespace SqlBulkSyncExport.Services.Export;

public sealed partial class SyncExportService
{
    private async Task<TableExportResult> ProcessTableAsync(
        ExportConfig config,
        SyncState state,
        string statePath,
        string outputFolder,
        bool seed,
        bool isFullSyncJob,
        string stamp,
        CsvWriteOptions csvOptionsBase,
        ResolvedTableExport table,
        SemaphoreSlim stateGate,
        CancellationToken cancellationToken)
    {
        var started = timeProvider.GetTimestamp();
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("Processing table {TableKey} ({SourceTable})...", table.Key, table.SourceTableName);

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(
            config.Source,
            applicationIntentReadOnly: false,
            cancellationToken);

        TableSyncState? tableState;
        await stateGate.WaitAsync(cancellationToken);
        try
        {
            state.Tables.TryGetValue(table.Key, out tableState);
        }
        finally
        {
            stateGate.Release();
        }

        var columns = await schemaService.GetColumnsAsync(
            connection,
            table.SourceTableName,
            config.CommandTimeoutSeconds,
            cancellationToken);

        TableVersion sourceVersion;
        ExportDecision decision;

        if (isFullSyncJob)
        {
            sourceVersion = await schemaService.GetUnixEpochVersionAsync(
                connection,
                table.SourceTableName,
                config.CommandTimeoutSeconds,
                cancellationToken);
            decision = ExportDecisionMaker.DecideFullSync(
                tableState,
                sourceVersion,
                config.FullSync!.IntervalMilliseconds);
        }
        else
        {
            sourceVersion = await schemaService.GetChangeTrackingVersionAsync(
                connection,
                table.SourceTableName,
                config.CommandTimeoutSeconds,
                cancellationToken);
            decision = ExportDecisionMaker.DecideChangeTracking(tableState, sourceVersion, seed && !isFullSyncJob);
        }

        logger.LogInformation(
            "Table {TableKey}: mode={Mode}, from={FromVersion}, to={ToVersion}",
            table.Key,
            decision.Mode,
            decision.FromVersion,
            decision.ToVersion);

        if (decision.Mode == ExportMode.Skip)
        {
            await PersistTableStateAsync(state, statePath, table.Key, sourceVersion, stateGate, cancellationToken);
            return new TableExportResult(table.Key, decision.Mode, 0, timeProvider.GetElapsedTime(started), OutputFileName: null);
        }

        SqlConnection readConnection = connection;
        SqlConnection? readonlyConnection = null;
        if (table.UseApplicationIntentReadOnly)
        {
            readonlyConnection = await connectionFactory.CreateOpenConnectionAsync(
                config.Source,
                applicationIntentReadOnly: true,
                cancellationToken);
            readConnection = readonlyConnection;
        }

        long totalRows;
        string outputFileName;
        try
        {
            if (decision.Mode == ExportMode.Full)
            {
                outputFileName = fileNameBuilder.BuildFullFileName(
                    table.TargetFileName,
                    stamp,
                    decision.ToVersion);
                var path = Path.Combine(outputFolder, outputFileName);
                totalRows = await ExportFullAsync(
                    readConnection,
                    table,
                    columns,
                    config.CommandTimeoutSeconds,
                    path,
                    csvOptionsBase,
                    config.ProgressLogBatchSize,
                    cancellationToken);
                logger.LogInformation("Wrote {Rows} rows to {Path}", totalRows, path);
            }
            else
            {
                outputFileName = fileNameBuilder.BuildDeltaFileName(
                    table.TargetFileName,
                    stamp,
                    decision.FromVersion,
                    decision.ToVersion);
                var changesPath = Path.Combine(outputFolder, outputFileName);
                var changeRows = await ExportDeltaAsync(
                    connection,
                    table.SourceTableName,
                    columns,
                    decision.FromVersion,
                    config.CommandTimeoutSeconds,
                    changesPath,
                    csvOptionsBase,
                    config.ProgressLogBatchSize,
                    cancellationToken);
                logger.LogInformation("Wrote {Rows} change rows to {Path}", changeRows, changesPath);
                totalRows = changeRows;

                if (table.WriteDeleted)
                {
                    var deletedName = fileNameBuilder.BuildDeletedFileName(
                        table.DeletedFileName,
                        stamp,
                        decision.FromVersion,
                        decision.ToVersion);
                    var deletedPath = Path.Combine(outputFolder, deletedName);
                    var deletedRows = await ExportDeletedAsync(
                        connection,
                        table.SourceTableName,
                        columns,
                        decision.FromVersion,
                        config.CommandTimeoutSeconds,
                        deletedPath,
                        csvOptionsBase,
                        config.ProgressLogBatchSize,
                        cancellationToken);
                    logger.LogInformation("Wrote {Rows} deleted PK rows to {Path}", deletedRows, deletedPath);
                    totalRows += deletedRows;
                    outputFileName = $"{outputFileName}, {deletedName}";
                }
            }
        }
        finally
        {
            if (readonlyConnection is not null)
            {
                await readonlyConnection.DisposeAsync();
            }
        }

        await PersistTableStateAsync(state, statePath, table.Key, sourceVersion, stateGate, cancellationToken);
        return new TableExportResult(table.Key, decision.Mode, totalRows, timeProvider.GetElapsedTime(started), outputFileName);
    }
}
