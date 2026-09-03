namespace SqlBulkSyncExport.Services.Export;

public sealed partial class SyncExportService(
    ISqlConnectionFactory connectionFactory,
    ISchemaService schemaService,
    ICsvExportWriter csvExportWriter,
    IExportFileNameBuilder fileNameBuilder,
    IYamlConfigStore yamlConfigStore,
    TimeProvider timeProvider,
    IAnsiConsole ansiConsole,
    ILogger<SyncExportService> logger) : ISyncExportService;
