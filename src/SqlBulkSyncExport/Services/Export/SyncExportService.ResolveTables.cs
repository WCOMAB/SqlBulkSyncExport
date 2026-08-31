using SqlBulkSyncExport.Helpers;

namespace SqlBulkSyncExport.Services.Export;

public sealed partial class SyncExportService
{
    private static List<ResolvedTableExport> ResolveTables(
        ExportConfig config,
        IReadOnlyCollection<string>? includeTables,
        IReadOnlyCollection<string>? excludeTables)
    {
        var include = includeTables is { Count: > 0 }
            ? new HashSet<string>(includeTables, StringComparer.OrdinalIgnoreCase)
            : null;
        var exclude = excludeTables is { Count: > 0 }
            ? new HashSet<string>(excludeTables, StringComparer.OrdinalIgnoreCase)
            : null;

        var result = new List<ResolvedTableExport>();
        foreach (var (key, table) in config.Tables)
        {
            if (include is not null && !include.Contains(key))
            {
                continue;
            }

            if (exclude is not null && exclude.Contains(key))
            {
                continue;
            }

            var targetFile = table.TargetFile is null
                ? OutputFileNames.DefaultTargetFile(key)
                : OutputFileNames.ValidateFileName(table.TargetFile, nameof(table.TargetFile));

            var writeDeleted = table.WriteDeleted ?? config.WriteDeleted;
            var deletedFile = table.DeletedFile is null
                ? OutputFileNames.DefaultDeletedFile(targetFile)
                : OutputFileNames.ValidateFileName(table.DeletedFile, nameof(table.DeletedFile));

            var useSnapshot = config.FullSync is not null
                ? config.FullSync.UseSnapshotIsolation
                : config.UseSnapshotIsolationSeed;
            var useReadOnly = config.FullSync is not null
                ? config.FullSync.UseApplicationIntentReadOnly
                : config.UseApplicationIntentReadOnlySeed;

            result.Add(new ResolvedTableExport(
                key,
                table.Source,
                targetFile,
                deletedFile,
                writeDeleted,
                useSnapshot,
                useReadOnly));
        }

        return result;
    }
}
