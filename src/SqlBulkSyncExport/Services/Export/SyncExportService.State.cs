namespace SqlBulkSyncExport.Services.Export;

public sealed partial class SyncExportService
{
    private static void UpsertState(
        SyncState state,
        string key,
        TableVersion sourceVersion,
        DateTimeOffset updated)
    {
        if (!state.Tables.TryGetValue(key, out var entry))
        {
            entry = new TableSyncState();
            state.Tables[key] = entry;
        }

        entry.CurrentVersion = sourceVersion.CurrentVersion;
        entry.MinValidVersion = sourceVersion.MinValidVersion;
        entry.Queried = sourceVersion.Queried;
        entry.Updated = updated;
    }

    private async Task PersistTableStateAsync(
        SyncState state,
        string statePath,
        string tableKey,
        TableVersion sourceVersion,
        SemaphoreSlim stateGate,
        CancellationToken cancellationToken)
    {
        await stateGate.WaitAsync(cancellationToken);
        try
        {
            UpsertState(state, tableKey, sourceVersion, timeProvider.GetUtcNow());
            yamlConfigStore.SaveState(statePath, state);
        }
        finally
        {
            stateGate.Release();
        }
    }
}
