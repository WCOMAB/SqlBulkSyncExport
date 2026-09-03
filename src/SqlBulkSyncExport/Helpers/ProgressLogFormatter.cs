using System.Globalization;

namespace SqlBulkSyncExport.Helpers;

public static class ProgressLogFormatter
{
    public static double PercentComplete(long rowsWritten, long estimatedTotalRows)
    {
        if (estimatedTotalRows <= 0 || rowsWritten <= 0)
        {
            return 0;
        }

        return Math.Min(100.0, rowsWritten * 100.0 / estimatedTotalRows);
    }

    public static TimeSpan? EstimateRemaining(TimeSpan elapsed, long rowsWritten, long estimatedTotalRows)
    {
        if (rowsWritten <= 0 || estimatedTotalRows <= rowsWritten)
        {
            return rowsWritten > 0 && estimatedTotalRows <= rowsWritten ? TimeSpan.Zero : null;
        }

        var remaining = estimatedTotalRows - rowsWritten;
        return TimeSpan.FromTicks(elapsed.Ticks * remaining / rowsWritten);
    }

    public static string FormatEta(TimeSpan? eta)
    {
        if (eta is null)
        {
            return "n/a";
        }

        if (eta.Value < TimeSpan.Zero)
        {
            return "n/a";
        }

        if (eta.Value.TotalHours >= 24)
        {
            return eta.Value.ToString(@"d\.hh\:mm\:ss", CultureInfo.InvariantCulture);
        }

        return eta.Value.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
    }
}
