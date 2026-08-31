namespace SqlBulkSyncExport.Models.State;

public sealed class SyncState
{
    public Dictionary<string, TableSyncState> Tables { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
