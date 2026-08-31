namespace SqlBulkSyncExport.Models.Config;

public sealed class SourceConfig
{
    public string ConnectionString { get; init; } = string.Empty;

    public bool EntraIdAuth { get; init; }

    public string? TenantId { get; init; }
}
