namespace SqlBulkSyncExport.Helpers;

public static class ProgressLogBatchResolver
{
    public const int FallbackBatchSize = 10_000;

    /// <summary>
    /// Resolves progress log interval and optional estimated total.
    /// <paramref name="configured"/> null = auto (tiered % of <paramref name="countedRows"/>);
    /// &lt;= 0 disables; &gt; 0 is a fixed interval without estimate.
    /// Auto tiers: &lt;1k → 50%, &lt;5k → 25%, &lt;10k → 20%, &lt;25k → 10%, &lt;250k → 5%, &lt;500k → 2%, else 1%.
    /// </summary>
    public static (int BatchSize, long? EstimatedTotalRows) Resolve(int? configured, long? countedRows)
    {
        if (configured is { } n)
        {
            return n <= 0 ? (0, null) : (n, null);
        }

        if (countedRows is null)
        {
            return (FallbackBatchSize, null);
        }

        if (countedRows.Value <= 0)
        {
            return (0, countedRows);
        }

        var stepDivisor = AutoStepDivisor(countedRows.Value);
        return ((int)Math.Max(1, countedRows.Value / stepDivisor), countedRows);
    }

    private static int AutoStepDivisor(long countedRows)
        => countedRows switch
        {
            < 1_000 => 2,        // 50%
            < 5_000 => 4,        // 25%
            < 10_000 => 5,       // 20%
            < 25_000 => 10,      // 10%
            < 250_000 => 20,     // 5%
            < 500_000 => 50,     // 2%
            _ => 100             // 1%
        };
}
