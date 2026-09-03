using SqlBulkSyncExport.Helpers;

namespace SqlBulkSyncExport.Tests;

public sealed class ProgressLogBatchResolverTests
{
    [Theory]
    [InlineData(50, 25)]           // <1k → 50%
    [InlineData(999, 499)]
    [InlineData(1_000, 250)]       // <5k → 25%
    [InlineData(4_999, 1_249)]
    [InlineData(5_000, 1_000)]     // <10k → 20%
    [InlineData(9_999, 1_999)]
    [InlineData(10_000, 1_000)]    // <25k → 10%
    [InlineData(24_999, 2_499)]
    [InlineData(25_000, 1_250)]    // <250k → 5%
    [InlineData(249_999, 12_499)]
    [InlineData(250_000, 5_000)]   // <500k → 2%
    [InlineData(499_999, 9_999)]
    [InlineData(500_000, 5_000)]   // else → 1%
    [InlineData(100_000, 5_000)]
    public void Resolve_Auto_UsesTieredPercentSteps(long count, int expectedBatch)
    {
        var (batch, estimated) = ProgressLogBatchResolver.Resolve(configured: null, countedRows: count);
        Assert.Equal(expectedBatch, batch);
        Assert.Equal(count, estimated);
    }

    [Fact]
    public void Resolve_Auto_ZeroCount_DisablesBatch()
    {
        var (batch, estimated) = ProgressLogBatchResolver.Resolve(configured: null, countedRows: 0);
        Assert.Equal(0, batch);
        Assert.Equal(0, estimated);
    }

    [Fact]
    public void Resolve_Auto_MissingCount_FallsBackWithoutEstimate()
    {
        var (batch, estimated) = ProgressLogBatchResolver.Resolve(configured: null, countedRows: null);
        Assert.Equal(ProgressLogBatchResolver.FallbackBatchSize, batch);
        Assert.Null(estimated);
    }

    [Fact]
    public void Resolve_Disabled_WhenNonPositive()
    {
        var (batch, estimated) = ProgressLogBatchResolver.Resolve(configured: 0, countedRows: 10_000);
        Assert.Equal(0, batch);
        Assert.Null(estimated);
    }

    [Fact]
    public void Resolve_Fixed_IgnoresCount()
    {
        var (batch, estimated) = ProgressLogBatchResolver.Resolve(configured: 5_000, countedRows: 10_000);
        Assert.Equal(5_000, batch);
        Assert.Null(estimated);
    }
}
