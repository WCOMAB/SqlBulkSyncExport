namespace SqlBulkSyncExport.Commands;

public sealed class SyncCommand(
    IYamlConfigStore yamlConfigStore,
    ISyncExportService syncExportService,
    ILogger<SyncCommand> logger) : AsyncCommand<SyncSettings>
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        SyncSettings settings,
        CancellationToken cancellationToken)
    {
        var config = yamlConfigStore.LoadConfig(settings.ConfigPath);
        var state = yamlConfigStore.LoadState(settings.StatePath);
        var parallelism = ParallelismResolver.Resolve(settings.Parallelism, config.Parallelism);

        logger.LogInformation(
            "Starting sync. Config={Config} State={State} Output={Output} Seed={Seed} Parallelism={Parallelism}",
            settings.ConfigPath,
            settings.StatePath,
            settings.OutputFolder,
            settings.Seed,
            parallelism);

        return await syncExportService.RunAsync(
            config,
            state,
            settings.StatePath,
            settings.OutputFolder,
            settings.Seed,
            settings.IncludeTables,
            settings.ExcludeTables,
            parallelism,
            cancellationToken);
    }
}
