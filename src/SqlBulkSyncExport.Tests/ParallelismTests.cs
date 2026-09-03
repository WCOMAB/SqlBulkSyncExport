using SqlBulkSyncExport.Commands.Settings;
using SqlBulkSyncExport.Helpers;
using SqlBulkSyncExport.Services.Config;

namespace SqlBulkSyncExport.Tests;

public sealed class ParallelismResolverTests
{
    [Theory]
    [InlineData(null, null, 1)]
    [InlineData(null, 4, 4)]
    [InlineData(2, null, 2)]
    [InlineData(2, 8, 2)]
    public void Resolve_CliOverridesYamlThenDefaultsToOne(int? cli, int? yaml, int expected)
    {
        Assert.Equal(expected, ParallelismResolver.Resolve(cli, yaml));
    }
}

public sealed class SyncSettingsParallelismTests
{
    [Fact]
    public void Validate_RejectsParallelismLessThanOne()
    {
        var settings = new SyncSettings
        {
            ConfigPath = "config.yml",
            StatePath = "state.yml",
            OutputFolder = "out",
            Parallelism = 0
        };

        var result = settings.Validate();

        Assert.True(result.Successful is false);
        Assert.Contains("Parallelism", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_AcceptsNullParallelism()
    {
        var settings = new SyncSettings
        {
            ConfigPath = "config.yml",
            StatePath = "state.yml",
            OutputFolder = "out",
            Parallelism = null
        };

        Assert.True(settings.Validate().Successful);
    }

    [Fact]
    public void Validate_AcceptsPositiveParallelism()
    {
        var settings = new SyncSettings
        {
            ConfigPath = "config.yml",
            StatePath = "state.yml",
            OutputFolder = "out",
            Parallelism = 4
        };

        Assert.True(settings.Validate().Successful);
    }
}

public sealed class YamlConfigParallelismTests
{
    [Fact]
    public void LoadConfig_ReadsParallelism()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sbs-config-{Guid.NewGuid():N}.yml");
        try
        {
            File.WriteAllText(
                path,
                """
                source:
                  connectionString: "Server=.;Database=db;Trusted_Connection=True;TrustServerCertificate=True"
                parallelism: 3
                tables:
                  customers:
                    source: dbo.Customers
                """);

            var config = new YamlConfigStore().LoadConfig(path);

            Assert.Equal(3, config.Parallelism);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadConfig_RejectsParallelismLessThanOne()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sbs-config-{Guid.NewGuid():N}.yml");
        try
        {
            File.WriteAllText(
                path,
                """
                source:
                  connectionString: "Server=.;Database=db;Trusted_Connection=True;TrustServerCertificate=True"
                parallelism: 0
                tables:
                  customers:
                    source: dbo.Customers
                """);

            var store = new YamlConfigStore();
            var ex = Assert.Throws<InvalidOperationException>(() => store.LoadConfig(path));
            Assert.Contains("parallelism", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
