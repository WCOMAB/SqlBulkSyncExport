using System.Globalization;
using Microsoft.Data.SqlClient;
using SqlBulkSyncExport.Helpers;

namespace SqlBulkSyncExport.Services.Export;

public sealed partial class SyncExportService
{
    public async Task<int> RunAsync(
        ExportConfig config,
        SyncState state,
        string statePath,
        string outputFolder,
        bool seed,
        IReadOnlyCollection<string>? includeTables,
        IReadOnlyCollection<string>? excludeTables,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputFolder);

        var stamp = timeProvider.GetUtcNow().UtcDateTime.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var separator = config.Separator[0];
        var newLine = NewLineParser.Parse(config.NewLine);
        var isFullSyncJob = config.FullSync is not null;

        if (seed && isFullSyncJob)
        {
            logger.LogWarning("--seed is ignored when fullSync is configured; interval full export rules apply.");
        }

        var tables = ResolveTables(config, includeTables, excludeTables);
        if (tables.Count == 0)
        {
            logger.LogWarning("No tables selected for export.");
            return 0;
        }

        await using var connection = await connectionFactory.CreateOpenConnectionAsync(
            config.Source,
            applicationIntentReadOnly: false,
            cancellationToken);

        var timeZoneId = await schemaService.GetCurrentTimeZoneIdAsync(
            connection,
            config.CommandTimeoutSeconds,
            cancellationToken);
        var sourceTimeZone = SourceTimeZoneResolver.Resolve(timeZoneId);
        logger.LogInformation("Source timezone: {TimeZoneId}", timeZoneId);

        var csvOptionsBase = new CsvWriteOptions(
            separator,
            config.IncludeHeader,
            newLine,
            sourceTimeZone);

        foreach (var table in tables)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogInformation("Processing table {TableKey} ({SourceTable})...", table.Key, table.SourceTableName);

            state.Tables.TryGetValue(table.Key, out var tableState);

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
                UpsertState(state, table.Key, sourceVersion, timeProvider.GetUtcNow());
                yamlConfigStore.SaveState(statePath, state);
                continue;
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

            try
            {
                if (decision.Mode == ExportMode.Full)
                {
                    var fileName = fileNameBuilder.BuildFullFileName(
                        table.TargetFileName,
                        stamp,
                        decision.ToVersion);
                    var path = Path.Combine(outputFolder, fileName);
                    var rows = await ExportFullAsync(
                        readConnection,
                        table,
                        columns,
                        config.CommandTimeoutSeconds,
                        path,
                        csvOptionsBase,
                        config.ProgressLogBatchSize,
                        cancellationToken);
                    logger.LogInformation("Wrote {Rows} rows to {Path}", rows, path);
                }
                else
                {
                    var changesName = fileNameBuilder.BuildDeltaFileName(
                        table.TargetFileName,
                        stamp,
                        decision.FromVersion,
                        decision.ToVersion);
                    var changesPath = Path.Combine(outputFolder, changesName);
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

            UpsertState(state, table.Key, sourceVersion, timeProvider.GetUtcNow());
            yamlConfigStore.SaveState(statePath, state);
        }

        return 0;
    }
}
