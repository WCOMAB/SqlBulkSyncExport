using SqlBulkSyncExport.Helpers;
using SqlBulkSyncExport.Models.State;
using SqlBulkSyncExport.Services.Config;

namespace SqlBulkSyncExport.Tests;

public sealed class YamlConfigStoreTests
{
    [Fact]
    public void LoadConfig_DefaultsIncludeHeaderTrueWhenOmitted()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sbs-config-{Guid.NewGuid():N}.yml");
        try
        {
            File.WriteAllText(
                path,
                """
                source:
                  connectionString: "Server=.;Database=db;Trusted_Connection=True;TrustServerCertificate=True"
                tables:
                  customers:
                    source: dbo.Customers
                """);

            var store = new YamlConfigStore();
            var config = store.LoadConfig(path);

            Assert.True(config.IncludeHeader);
            Assert.False(config.WriteDeleted);
            Assert.Null(config.Parallelism);
            Assert.Equal(",", config.Separator);
            Assert.Equal("\r\n", config.NewLine);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadConfig_RejectsTargetFileWithPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sbs-config-{Guid.NewGuid():N}.yml");
        try
        {
            File.WriteAllText(
                path,
                """
                source:
                  connectionString: "Server=.;Database=db;Trusted_Connection=True;TrustServerCertificate=True"
                tables:
                  customers:
                    source: dbo.Customers
                    targetFile: out/customers.csv
                """);

            var store = new YamlConfigStore();
            Assert.Throws<ArgumentException>(() => store.LoadConfig(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SaveAndLoadState_RoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sbs-state-{Guid.NewGuid():N}.yml");
        try
        {
            var store = new YamlConfigStore();
            var state = new SyncState
            {
                Tables =
                {
                    ["customers"] = new TableSyncState
                    {
                        CurrentVersion = 42,
                        MinValidVersion = 1,
                        Queried = DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                        Updated = DateTimeOffset.Parse("2026-01-01T00:00:01Z")
                    }
                }
            };

            store.SaveState(path, state);
            var loaded = store.LoadState(path);

            Assert.Equal(42, loaded.Tables["customers"].CurrentVersion);
            Assert.Equal(1, loaded.Tables["customers"].MinValidVersion);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}

public sealed class NewLineParserTests
{
    [Theory]
    [InlineData(null, "\r\n")]
    [InlineData("", "\r\n")]
    [InlineData("crlf", "\r\n")]
    [InlineData("lf", "\n")]
    [InlineData("\r\n", "\r\n")]
    [InlineData("\n", "\n")]
    public void Parse_ResolvesAliases(string? input, string expected)
    {
        Assert.Equal(expected, NewLineParser.Parse(input));
    }
}
