public partial class Program
{
    static partial void AddServices(IServiceCollection services)
    {
        services
            .AddSingleton(TimeProvider.System)
            .AddSingleton<IYamlConfigStore, YamlConfigStore>()
            .AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>()
            .AddSingleton<ISchemaService, SchemaService>()
            .AddSingleton<ICsvExportWriter, SepCsvExportWriter>()
            .AddSingleton<IExportFileNameBuilder, ExportFileNameBuilder>()
            .AddSingleton<ISyncExportService, SyncExportService>();
    }

    static partial void ConfigureApp(AppServiceConfig appServiceConfig)
    {
        appServiceConfig.SetApplicationName("sqlbulksyncexport");
        appServiceConfig.AddCommand<SyncCommand>("sync")
            .WithDescription("Export SQL Server change-tracking (or full-sync) deltas to CSV files.")
            .WithExample(["sync", "config.yml", "state.yml", "output"]);
    }
}
