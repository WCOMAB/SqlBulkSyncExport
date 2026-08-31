using Microsoft.Data.SqlClient;

namespace SqlBulkSyncExport.Services.Sql;

public interface ISchemaService
{
    Task<IReadOnlyList<Column>> GetColumnsAsync(
        SqlConnection connection,
        string tableName,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken);

    Task<TableVersion> GetChangeTrackingVersionAsync(
        SqlConnection connection,
        string tableName,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken);

    Task<TableVersion> GetUnixEpochVersionAsync(
        SqlConnection connection,
        string tableName,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken);

    Task<string> GetCurrentTimeZoneIdAsync(
        SqlConnection connection,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken);
}
