using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SqlBulkSyncExport.Services.Config;

public sealed class YamlConfigStore : IYamlConfigStore
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    public ExportConfig LoadConfig(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Config file not found: {path}", path);
        }

        var yaml = File.ReadAllText(path);
        var config = Deserializer.Deserialize<ExportConfig>(yaml)
            ?? throw new InvalidOperationException($"Failed to deserialize config: {path}");

        if (string.IsNullOrWhiteSpace(config.Source.ConnectionString))
        {
            throw new InvalidOperationException("source.connectionString is required.");
        }

        if (config.Tables.Count == 0)
        {
            throw new InvalidOperationException("At least one table must be configured under tables.");
        }

        if (string.IsNullOrWhiteSpace(config.Separator) || config.Separator.Length != 1)
        {
            throw new InvalidOperationException("separator must be a single character.");
        }

        if (config.FullSync is { IntervalMilliseconds: <= 0 })
        {
            throw new InvalidOperationException("fullSync.intervalMilliseconds must be greater than 0.");
        }

        if (config.Parallelism is <= 0)
        {
            throw new InvalidOperationException("parallelism must be greater than 0.");
        }

        foreach (var (key, table) in config.Tables)
        {
            if (string.IsNullOrWhiteSpace(table.Source))
            {
                throw new InvalidOperationException($"tables.{key}.source is required.");
            }

            if (table.TargetFile is not null)
            {
                Helpers.OutputFileNames.ValidateFileName(table.TargetFile, $"tables.{key}.targetFile");
            }

            if (table.DeletedFile is not null)
            {
                Helpers.OutputFileNames.ValidateFileName(table.DeletedFile, $"tables.{key}.deletedFile");
            }
        }

        return config;
    }

    public SyncState LoadState(string path)
    {
        if (!File.Exists(path))
        {
            return new SyncState();
        }

        var yaml = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return new SyncState();
        }

        return Deserializer.Deserialize<SyncState>(yaml) ?? new SyncState();
    }

    public void SaveState(string path, SyncState state)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var yaml = Serializer.Serialize(state);
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, yaml);
        File.Move(tempPath, path, overwrite: true);
    }
}
