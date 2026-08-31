namespace SqlBulkSyncExport.Models.Schema;

public sealed record TableVersion(
    string TableName,
    long CurrentVersion,
    long MinValidVersion,
    DateTimeOffset Queried);
