using System.Data.Common;

namespace SqlBulkSyncExport.Services.Csv;

public interface ICsvExportWriter
{
    Task<long> WriteAsync(
        string filePath,
        DbDataReader reader,
        CsvWriteOptions options,
        CancellationToken cancellationToken);
}
