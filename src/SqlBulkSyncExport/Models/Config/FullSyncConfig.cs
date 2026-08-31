namespace SqlBulkSyncExport.Models.Config;

public sealed class FullSyncConfig
{
    public long IntervalMilliseconds { get; init; }

    public bool UseSnapshotIsolation { get; init; }

    public bool UseApplicationIntentReadOnly { get; init; }
}
