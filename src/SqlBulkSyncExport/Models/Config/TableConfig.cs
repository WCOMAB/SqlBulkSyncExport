namespace SqlBulkSyncExport.Models.Config;

public sealed class TableConfig
{
    public string Source { get; init; } = string.Empty;

    /// <summary>Filename with extension, no path. Default: "{key}.csv".</summary>
    public string? TargetFile { get; init; }

    /// <summary>When null, inherits <see cref="ExportConfig.WriteDeleted"/>.</summary>
    public bool? WriteDeleted { get; init; }

    /// <summary>Filename with extension, no path. Default: "{targetStem}.deleted{ext}".</summary>
    public string? DeletedFile { get; init; }
}
