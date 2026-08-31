namespace SqlBulkSyncExport.Models;

public sealed record ResolvedTableExport(
    string Key,
    string SourceTableName,
    string TargetFileName,
    string DeletedFileName,
    bool WriteDeleted,
    bool UseSnapshotIsolation,
    bool UseApplicationIntentReadOnly);
