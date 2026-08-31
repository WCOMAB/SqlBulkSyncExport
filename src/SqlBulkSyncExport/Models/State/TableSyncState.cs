namespace SqlBulkSyncExport.Models.State;

public sealed class TableSyncState
{
    public long CurrentVersion { get; set; } = -1;

    public long MinValidVersion { get; set; } = -1;

    public DateTimeOffset? Queried { get; set; }

    public DateTimeOffset? Updated { get; set; }
}
