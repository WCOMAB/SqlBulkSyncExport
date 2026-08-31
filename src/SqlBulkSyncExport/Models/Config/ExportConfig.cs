namespace SqlBulkSyncExport.Models.Config;

public sealed class ExportConfig
{
    public SourceConfig Source { get; init; } = new();

    public string Separator { get; init; } = ",";

    /// <summary>Defaults to true when omitted from YAML.</summary>
    public bool IncludeHeader { get; init; } = true;

    public string NewLine { get; init; } = "\r\n";

    public bool WriteDeleted { get; init; }

    public int CommandTimeoutSeconds { get; init; } = 180;

    /// <summary>Log progress every N written rows. Defaults to 10000 when omitted. Use 0 or less to disable.</summary>
    public int ProgressLogBatchSize { get; init; } = 10_000;

    public FullSyncConfig? FullSync { get; init; }

    public bool UseSnapshotIsolationSeed { get; init; }

    public bool UseApplicationIntentReadOnlySeed { get; init; }

    public Dictionary<string, TableConfig> Tables { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
