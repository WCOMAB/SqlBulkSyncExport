using System.Data.Common;
using System.Globalization;
using System.Text;
using nietras.SeparatedValues;
using SqlBulkSyncExport.Helpers;

namespace SqlBulkSyncExport.Services.Csv;

public sealed class SepCsvExportWriter(ILogger<SepCsvExportWriter> logger) : ICsvExportWriter
{
    private const string DateTimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fffK";
    private const string DateTimeUnspecifiedFormat = "yyyy-MM-dd'T'HH:mm:ss.fff";
    private const string DateOnlyFormat = "yyyy-MM-dd";
    private const string TimeOnlyFormat = "HH:mm:ss.fffK";
    private const string TimeSpanFormat = @"hh\:mm\:ss\.fff";

    public async Task<long> WriteAsync(
        string filePath,
        DbDataReader reader,
        CsvWriteOptions options,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var file = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 64 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var buffered = new BufferedStream(file, 64 * 1024);
        await using var text = new StreamWriter(
            buffered,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            bufferSize: 64 * 1024)
        {
            NewLine = options.NewLine
        };

        var fieldCount = reader.FieldCount;
        var names = CacheFieldNames(reader, fieldCount);

        await using var writer = Sep.New(options.Separator).Writer(o => o with
        {
            WriteHeader = options.IncludeHeader,
            CultureInfo = CultureInfo.InvariantCulture
        }).To(text, leaveOpen: true);

        // Header.Add order defines Sep ordinals 0..n-1 to match reader ordinals for the reader lifetime.
        if (options.IncludeHeader)
        {
            writer.Header.Add(names);
        }

        var batchSize = options.ProgressLogBatchSize;
        var estimatedTotalRows = options.EstimatedTotalRows;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        long rows = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            rows++;

            Exception? cellFailure = null;
            var failedOrdinal = -1;

            using (var row = writer.NewRow())
            {
                for (var i = 0; i < fieldCount; i++)
                {
                    if (cellFailure is not null)
                    {
                        row[i].Set(string.Empty);
                        continue;
                    }

                    try
                    {
                        if (reader.IsDBNull(i))
                        {
                            row[i].Set(string.Empty);
                        }
                        else
                        {
                            SetValue(row[i], reader, i, options);
                        }
                    }
                    catch (Exception ex)
                    {
                        cellFailure = ex;
                        failedOrdinal = i;
                        try
                        {
                            row[i].Set(string.Empty);
                        }
                        catch
                        {
                            // Ensure EndRow does not mask the original cell failure.
                        }
                    }
                }
            }

            if (cellFailure is not null)
            {
                throw new InvalidOperationException(
                    $"Failed writing CSV column '{names[failedOrdinal]}' (ordinal {failedOrdinal}) at data row {rows} for '{filePath}'.",
                    cellFailure);
            }

            if (batchSize > 0 && rows % batchSize == 0)
            {
                LogProgress(rows, filePath, estimatedTotalRows, stopwatch.Elapsed);
            }
        }

        return rows;
    }

    private void LogProgress(long rows, string filePath, long? estimatedTotalRows, TimeSpan elapsed)
    {
        if (estimatedTotalRows is long estimated && estimated > 0)
        {
            var percent = ProgressLogFormatter.PercentComplete(rows, estimated);
            var eta = ProgressLogFormatter.EstimateRemaining(elapsed, rows, estimated);
            logger.LogInformation(
                "Wrote {Rows}/{Estimated} rows ({Percent:0}%) to {Path}; ETA {Eta}",
                rows,
                estimated,
                percent,
                filePath,
                ProgressLogFormatter.FormatEta(eta));
            return;
        }

        logger.LogInformation(
            "Wrote {Rows} rows to {Path}",
            rows,
            filePath);
    }

    private static string[] CacheFieldNames(DbDataReader reader, int fieldCount)
    {
        var names = new string[fieldCount];
        var seen = new HashSet<string>(fieldCount, StringComparer.Ordinal);
        for (var i = 0; i < fieldCount; i++)
        {
            var name = reader.GetName(i);
            if (!seen.Add(name))
            {
                throw new InvalidOperationException(
                    $"Duplicate column name '{name}' at reader ordinal {i}. CSV export requires unique field names.");
            }

            names[i] = name;
        }

        return names;
    }

    private static void SetValue(
        SepWriter.Col col,
        DbDataReader reader,
        int ordinal,
        CsvWriteOptions options)
    {
        var value = reader.GetValue(ordinal);
        switch (value)
        {
            case string s:
                col.Set(s);
                break;
            case byte[] bytes:
                col.Set(Convert.ToBase64String(bytes));
                break;
            case bool b:
                col.Set(b ? "True" : "False");
                break;
            case byte b:
                col.Format(b);
                break;
            case short s:
                col.Format(s);
                break;
            case int i:
                col.Format(i);
                break;
            case long l:
                col.Format(l);
                break;
            case float f:
                col.Format(f);
                break;
            case double d:
                col.Format(d);
                break;
            case decimal m:
                col.Format(m);
                break;
            case Guid g:
                col.Format(g);
                break;
            case DateTime dt:
                col.Set(FormatDateTime(dt, options.SourceTimeZone));
                break;
            case DateTimeOffset dto:
                col.Set(dto.ToString(DateTimeFormat, CultureInfo.InvariantCulture));
                break;
            case TimeSpan ts:
                col.Set(ts.ToString(TimeSpanFormat, CultureInfo.InvariantCulture));
                break;
            case DateOnly dateOnly:
                col.Set(dateOnly.ToString(DateOnlyFormat, CultureInfo.InvariantCulture));
                break;
            case TimeOnly timeOnly:
                col.Set(timeOnly.ToString(TimeOnlyFormat, CultureInfo.InvariantCulture));
                break;
            default:
                col.Set(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                break;
        }
    }

    private static string FormatDateTime(DateTime dt, TimeZoneInfo sourceTimeZone)
    {
        if (dt.Kind == DateTimeKind.Utc)
        {
            return dt.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
        }

        if (TryToSourceDateTimeOffset(dt, sourceTimeZone, out var dto))
        {
            return dto.ToString(DateTimeFormat, CultureInfo.InvariantCulture);
        }

        // Offset would push outside DateTimeOffset range (e.g. 0001-01-01 with +02:00).
        return DateTime.SpecifyKind(dt, DateTimeKind.Unspecified)
            .ToString(DateTimeUnspecifiedFormat, CultureInfo.InvariantCulture);
    }

    private static bool TryToSourceDateTimeOffset(
        DateTime dt,
        TimeZoneInfo sourceTimeZone,
        out DateTimeOffset dto)
    {
        try
        {
            dto = dt.Kind == DateTimeKind.Local
                ? new DateTimeOffset(dt)
                : new DateTimeOffset(
                    DateTime.SpecifyKind(dt, DateTimeKind.Unspecified),
                    sourceTimeZone.GetUtcOffset(dt));
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            dto = default;
            return false;
        }
    }
}
