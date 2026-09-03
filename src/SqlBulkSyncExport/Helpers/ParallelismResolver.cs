namespace SqlBulkSyncExport.Helpers;

public static class ParallelismResolver
{
    /// <summary>CLI overrides YAML; when both omitted, defaults to 1.</summary>
    public static int Resolve(int? cliParallelism, int? yamlParallelism)
        => cliParallelism ?? yamlParallelism ?? 1;
}
