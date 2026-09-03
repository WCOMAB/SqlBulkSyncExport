namespace SqlBulkSyncExport.Services.Export;

public interface ISyncExportService
{
    Task<int> RunAsync(
        ExportConfig config,
        SyncState state,
        string statePath,
        string outputFolder,
        bool seed,
        IReadOnlyCollection<string>? includeTables,
        IReadOnlyCollection<string>? excludeTables,
        int parallelism,
        CancellationToken cancellationToken);
}
