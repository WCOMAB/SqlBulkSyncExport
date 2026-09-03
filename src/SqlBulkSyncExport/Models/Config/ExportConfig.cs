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

    /// <summary>
    /// Log progress every N written rows.
    /// Null/omitted = auto (~1% via COUNT, with percent and ETA).
    /// Use 0 or less to disable. Positive = fixed interval without COUNT/%/ETA.
    /// </summary>
    public int? ProgressLogBatchSize { get; init; }

    /// <summary>
    /// Max number of tables to export concurrently.
    /// Null/omitted = use CLI <c>--parallelism</c> if set, otherwise 1.
    /// Must be greater than 0 when set. CLI overrides this value when specified.
    /// </summary>
    public int? Parallelism { get; init; }

    public FullSyncConfig? FullSync { get; init; }

    public bool UseSnapshotIsolationSeed { get; init; }

    public bool UseApplicationIntentReadOnlySeed { get; init; }

    public Dictionary<string, TableConfig> Tables { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
