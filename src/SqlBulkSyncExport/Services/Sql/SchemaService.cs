using Microsoft.Data.SqlClient;
using SqlBulkSyncExport.Helpers;

namespace SqlBulkSyncExport.Services.Sql;

public sealed class SchemaService : ISchemaService
{
    public async Task<IReadOnlyList<Column>> GetColumnsAsync(
        SqlConnection connection,
        string tableName,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = SqlStatements.GetColumnsStatement();
        command.CommandTimeout = commandTimeoutSeconds;
        command.Parameters.AddWithValue("@TableName", tableName);

        var columns = new List<Column>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(new Column(
                Name: reader.GetString(reader.GetOrdinal("Name")),
                QuoteName: reader.GetString(reader.GetOrdinal("QuoteName")),
                Type: reader.GetString(reader.GetOrdinal("Type")),
                IsIdentity: reader.GetBoolean(reader.GetOrdinal("IsIdentity")),
                IsPrimary: reader.GetBoolean(reader.GetOrdinal("IsPrimary")),
                IsNullable: reader.GetBoolean(reader.GetOrdinal("IsNullable")),
                Collation: reader.IsDBNull(reader.GetOrdinal("Collation"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("Collation"))));
        }

        if (columns.Count == 0)
        {
            throw new InvalidOperationException($"No exportable columns found for table {tableName}.");
        }

        return columns;
    }

    public async Task<TableVersion> GetChangeTrackingVersionAsync(
        SqlConnection connection,
        string tableName,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = SqlStatements.GetChangeTrackingVersionStatement(tableName);
        command.CommandTimeout = commandTimeoutSeconds;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                $"Table {tableName} is not enabled for change tracking (or OBJECT_ID did not resolve).");
        }

        return ReadVersion(reader);
    }

    public async Task<TableVersion> GetUnixEpochVersionAsync(
        SqlConnection connection,
        string tableName,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = SqlStatements.GetUnixEpochMillisecondsVersionStatement(tableName);
        command.CommandTimeout = commandTimeoutSeconds;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException($"Failed to read source clock version for {tableName}.");
        }

        return ReadVersion(reader);
    }

    public async Task<string> GetCurrentTimeZoneIdAsync(
        SqlConnection connection,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CURRENT_TIMEZONE_ID();";
        command.CommandTimeout = commandTimeoutSeconds;

        try
        {
            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (result is null or DBNull || result is not string timeZoneId || string.IsNullOrWhiteSpace(timeZoneId))
            {
                throw new InvalidOperationException(
                    "CURRENT_TIMEZONE_ID() returned no value. SQL Server 2022+ (or equivalent Azure SQL) is required.");
            }

            return timeZoneId;
        }
        catch (SqlException ex)
        {
            throw new InvalidOperationException(
                "Failed to read CURRENT_TIMEZONE_ID(). SQL Server 2022+ (or equivalent Azure SQL) is required.",
                ex);
        }
    }

    private static TableVersion ReadVersion(SqlDataReader reader)
        => new(
            TableName: reader.GetString(reader.GetOrdinal("TableName")),
            CurrentVersion: reader.GetInt64(reader.GetOrdinal("CurrentVersion")),
            MinValidVersion: reader.GetInt64(reader.GetOrdinal("MinValidVersion")),
            Queried: reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("Queried")));
}
