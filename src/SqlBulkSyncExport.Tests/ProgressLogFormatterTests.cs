using SqlBulkSyncExport.Helpers;

namespace SqlBulkSyncExport.Tests;

public sealed class ProgressLogFormatterTests
{
    [Fact]
    public void PercentComplete_CapsAt100()
    {
        Assert.Equal(50, ProgressLogFormatter.PercentComplete(50, 100));
        Assert.Equal(100, ProgressLogFormatter.PercentComplete(150, 100));
        Assert.Equal(0, ProgressLogFormatter.PercentComplete(0, 100));
    }

    [Fact]
    public void EstimateRemaining_ScalesWithElapsed()
    {
        var eta = ProgressLogFormatter.EstimateRemaining(TimeSpan.FromSeconds(10), rowsWritten: 25, estimatedTotalRows: 100);
        Assert.Equal(TimeSpan.FromSeconds(30), eta);
    }

    [Fact]
    public void EstimateRemaining_WhenComplete_IsZero()
    {
        Assert.Equal(TimeSpan.Zero, ProgressLogFormatter.EstimateRemaining(TimeSpan.FromSeconds(5), 100, 100));
    }

    [Fact]
    public void FormatEta_UsesClockFormat()
    {
        Assert.Equal("n/a", ProgressLogFormatter.FormatEta(null));
        Assert.Equal("00:01:30", ProgressLogFormatter.FormatEta(TimeSpan.FromSeconds(90)));
    }
}
