namespace SqlBulkSyncExport.Services.Csv;

public sealed record CsvWriteOptions(
    char Separator,
    bool IncludeHeader,
    string NewLine,
    TimeZoneInfo SourceTimeZone,
    int ProgressLogBatchSize = 10_000);
