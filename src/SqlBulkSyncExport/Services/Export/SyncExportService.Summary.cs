using System.Collections.Concurrent;
using System.Globalization;

namespace SqlBulkSyncExport.Services.Export;

public sealed partial class SyncExportService
{
    private sealed record TableExportResult(
        string Key,
        ExportMode Mode,
        long Rows,
        TimeSpan Duration,
        string? OutputFileName);

    private void WriteSyncSummary(
        IReadOnlyList<ResolvedTableExport> tables,
        ConcurrentDictionary<string, TableExportResult> results,
        TimeSpan syncDuration,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt)
    {
        var summary = new Table()
                       .Expand();
        summary.AddColumn("Table");
        summary.AddColumn("File");
        summary.AddColumn(new TableColumn("Mode"));
        summary.AddColumn(new TableColumn("Rows") { Alignment = Justify.Right });
        summary.AddColumn(new TableColumn("Duration") { Alignment = Justify.Right });

        long totalRows = 0;
        foreach (var table in tables)
        {
            var result = results[table.Key];
            totalRows += result.Rows;
            summary.AddRow(
                result.Key,
                result.OutputFileName ?? string.Empty,
                result.Mode.ToString(),
                result.Rows.ToString(CultureInfo.InvariantCulture),
                FormatDuration(result.Duration));
        }

        summary.Columns[0].Footer = new Text("Total");
        summary.Columns[1].Footer = Text.Empty;
        summary.Columns[2].Footer = Text.Empty;
        summary.Columns[3].Footer = new Text(totalRows.ToString(CultureInfo.InvariantCulture));
        summary.Columns[4].Footer = new Text(FormatDuration(syncDuration));

        var panel = new Panel(summary)
            .Header($"Sync summary ({FormatTimestamp(startedAt)} → {FormatTimestamp(endedAt)})", Justify.Center)
            .Border(BoxBorder.Square)
            .Padding(1, 1)
            .Expand();

        ansiConsole.WriteLine();
        ansiConsole.Write(panel);
    }

    private static string FormatDuration(TimeSpan duration)
        => duration.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);

    private static string FormatTimestamp(DateTimeOffset value)
        => value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "Z";
}
