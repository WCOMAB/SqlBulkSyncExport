using System.Collections.Concurrent;
using System.Globalization;

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
        int parallelism,
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

        string timeZoneId;
        await using (var connection = await connectionFactory.CreateOpenConnectionAsync(
            config.Source,
            applicationIntentReadOnly: false,
            cancellationToken))
        {
            timeZoneId = await schemaService.GetCurrentTimeZoneIdAsync(
                connection,
                config.CommandTimeoutSeconds,
                cancellationToken);
        }

        var sourceTimeZone = SourceTimeZoneResolver.Resolve(timeZoneId);
        logger.LogInformation("Source timezone: {TimeZoneId}", timeZoneId);

        var csvOptionsBase = new CsvWriteOptions(
            separator,
            config.IncludeHeader,
            newLine,
            sourceTimeZone);

        using var stateGate = new SemaphoreSlim(1, 1);
        var results = new ConcurrentDictionary<string, TableExportResult>(StringComparer.OrdinalIgnoreCase);
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = parallelism,
            CancellationToken = cancellationToken
        };

        var syncStartedAt = timeProvider.GetUtcNow();
        var syncStarted = timeProvider.GetTimestamp();
        await Parallel.ForEachAsync(
            tables,
            parallelOptions,
            async (table, ct) =>
            {
                var result = await ProcessTableAsync(
                    config,
                    state,
                    statePath,
                    outputFolder,
                    seed,
                    isFullSyncJob,
                    stamp,
                    csvOptionsBase,
                    table,
                    stateGate,
                    ct);
                results[table.Key] = result;
            });
        var syncDuration = timeProvider.GetElapsedTime(syncStarted);
        var syncEndedAt = timeProvider.GetUtcNow();

        WriteSyncSummary(tables, results, syncDuration, syncStartedAt, syncEndedAt);
        return 0;
    }
}
