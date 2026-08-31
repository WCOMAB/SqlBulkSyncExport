using Microsoft.Data.SqlClient;

namespace SqlBulkSyncExport.Services.Sql;

public interface ISqlConnectionFactory
{
    Task<SqlConnection> CreateOpenConnectionAsync(
        SourceConfig source,
        bool applicationIntentReadOnly,
        CancellationToken cancellationToken);
}
