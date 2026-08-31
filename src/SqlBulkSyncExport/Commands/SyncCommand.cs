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
        logger.LogInformation(
            "Starting sync. Config={Config} State={State} Output={Output} Seed={Seed}",
            settings.ConfigPath,
            settings.StatePath,
            settings.OutputFolder,
            settings.Seed);

        var config = yamlConfigStore.LoadConfig(settings.ConfigPath);
        var state = yamlConfigStore.LoadState(settings.StatePath);

        return await syncExportService.RunAsync(
            config,
            state,
            settings.StatePath,
            settings.OutputFolder,
            settings.Seed,
            settings.IncludeTables,
            settings.ExcludeTables,
            cancellationToken);
    }
}
