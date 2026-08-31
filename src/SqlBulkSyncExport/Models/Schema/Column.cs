namespace SqlBulkSyncExport.Models.Schema;

public sealed record Column(
    string Name,
    string QuoteName,
    string Type,
    bool IsIdentity,
    bool IsPrimary,
    bool IsNullable,
    string? Collation);
