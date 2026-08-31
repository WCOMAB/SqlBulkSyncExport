namespace SqlBulkSyncExport.Services.Config;

public interface IYamlConfigStore
{
    ExportConfig LoadConfig(string path);

    SyncState LoadState(string path);

    void SaveState(string path, SyncState state);
}
