namespace SqlBulkSyncExport.Models;

public sealed record ExportDecision(
    ExportMode Mode,
    long FromVersion,
    long ToVersion,
    bool IsFullSyncMode);
