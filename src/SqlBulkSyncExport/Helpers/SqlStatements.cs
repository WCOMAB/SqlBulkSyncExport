using SqlBulkSyncExport.Models.Schema;

namespace SqlBulkSyncExport.Helpers;

public static class SqlStatements
{
    public static string GetChangeTrackingVersionStatement(string tableName)
        => $"""
            SELECT  '{EscapeLiteral(tableName)}' AS TableName,
                    CHANGE_TRACKING_CURRENT_VERSION() AS CurrentVersion,
                    CHANGE_TRACKING_MIN_VALID_VERSION(ctt.object_id) AS MinValidVersion,
                    SYSDATETIMEOFFSET() AS Queried
                FROM sys.change_tracking_tables AS ctt
                WHERE ctt.object_id = OBJECT_ID(N'{EscapeLiteral(tableName)}')
            """;

    public static string GetUnixEpochMillisecondsVersionStatement(string tableName)
        => $"""
            SELECT  '{EscapeLiteral(tableName)}' AS TableName,
                    DATEDIFF_BIG(
                        MILLISECOND,
                        '1970-01-01T00:00:00+00:00',
                        SWITCHOFFSET(SYSDATETIMEOFFSET(), '+00:00')
                    ) AS CurrentVersion,
                    CAST(0 AS bigint) AS MinValidVersion,
                    SYSDATETIMEOFFSET() AS Queried
            """;

    public static string GetColumnsStatement()
        => """
            SELECT  c.Name              AS Name,
                    QUOTENAME(c.Name)   AS QuoteName,
                    tn.Type             AS Type,
                    c.is_identity       AS IsIdentity,
                    tn.IsPrimary        AS IsPrimary,
                    c.is_nullable       AS IsNullable,
                    c.collation_name    AS Collation
                FROM sys.columns c
                    INNER JOIN sys.types tp ON c.user_type_id = tp.user_type_id
                    CROSS APPLY (
                        SELECT CASE
                                    WHEN tp.name IN ('nvarchar')
                                        THEN tp.name + '(' + CASE c.max_length WHEN -1 THEN 'max' ELSE CAST(c.max_length / 2 AS nvarchar(max)) END + ')'
                                    WHEN tp.name IN ('varchar')
                                        THEN tp.name + '(' + CASE c.max_length WHEN -1 THEN 'max' ELSE CAST(c.max_length AS varchar(max)) END + ')'
                                    WHEN tp.name IN ('varbinary', 'binary')
                                        THEN tp.name + '(' + CASE c.max_length WHEN -1 THEN 'max' ELSE CAST(c.max_length AS varchar(max)) END + ')'
                                    WHEN tp.name IN ('char')
                                        THEN tp.name + '(' + CAST(c.max_length AS varchar(max)) + ')'
                                    WHEN tp.name IN ('nchar')
                                        THEN tp.name + '(' + CAST(c.max_length / 2 AS nvarchar(max)) + ')'
                                    WHEN tp.name = 'decimal'
                                        THEN tp.name + '(' + CAST(c.precision AS nvarchar(max)) + ', ' + CAST(c.scale AS nvarchar(max)) + ')'
                                    WHEN tp.name IN ('datetime2', 'datetimeoffset', 'time')
                                        THEN tp.name + '(' + CAST(c.scale AS nvarchar(max)) + ')'
                                    ELSE tp.name
                                END AS Type,
                                CASE WHEN EXISTS (
                                        SELECT 1
                                            FROM sys.indexes i
                                                INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id
                                                    AND i.index_id = ic.index_id
                                                    AND c.column_id = ic.column_id
                                            WHERE i.is_primary_key = 1
                                                AND i.object_id = c.object_id)
                                    THEN CAST(1 AS bit)
                                    ELSE CAST(0 AS bit)
                                END AS IsPrimary
                    ) tn
                WHERE object_id = OBJECT_ID(@TableName)
                    AND c.is_computed = 0
                    AND tp.name <> 'timestamp'
            """;

    public static string GetSelectAllStatement(
        string sourceTableName,
        IReadOnlyList<Column> columns,
        bool useSnapshotIsolation)
    {
        EnsureColumns(sourceTableName, columns);

        var orderBy = columns.Any(c => c.IsPrimary && c.IsIdentity)
            ? "ORDER BY " + string.Join(
                ", ",
                columns.Where(c => c.IsPrimary && c.IsIdentity).Select(c => c.QuoteName + " ASC"))
            : string.Empty;

        return $"""
            SELECT  {string.Join(",\r\n        ", columns.Select(c => c.QuoteName))}
                FROM {sourceTableName}{(useSnapshotIsolation ? string.Empty : " WITH(NOLOCK)")}
                {orderBy}
            """;
    }

    public static string GetNewOrUpdatedSelectStatement(
        string sourceTableName,
        IReadOnlyList<Column> columns)
    {
        EnsureColumns(sourceTableName, columns);
        EnsurePrimaryKey(sourceTableName, columns);

        var selectList = string.Join(
            ",\r\n        ",
            columns.Select(c => "t." + c.QuoteName));
        var join = string.Join(
            " AND\r\n        ",
            columns.Where(c => c.IsPrimary).Select(c => $"t.{c.QuoteName} = ct.{c.QuoteName}"));

        return $"""
            SELECT  {selectList}
                FROM CHANGETABLE(CHANGES {sourceTableName}, @FromVersion) AS ct
                    INNER JOIN {sourceTableName} AS t WITH(NOLOCK) ON {join}
            """;
    }

    public static string GetDeletedPrimaryKeysSelectStatement(
        string sourceTableName,
        IReadOnlyList<Column> columns)
    {
        EnsurePrimaryKey(sourceTableName, columns);

        var selectList = string.Join(
            ",\r\n        ",
            columns.Where(c => c.IsPrimary).Select(c => "ct." + c.QuoteName));

        return $"""
            SELECT  {selectList}
                FROM CHANGETABLE(CHANGES {sourceTableName}, @FromVersion) AS ct
                WHERE ct.SYS_CHANGE_OPERATION = N'D'
            """;
    }

    private static void EnsureColumns(string sourceTableName, IReadOnlyList<Column> columns)
    {
        if (columns.Count == 0)
        {
            throw new InvalidOperationException($"Columns for table {sourceTableName} are missing.");
        }
    }

    private static void EnsurePrimaryKey(string sourceTableName, IReadOnlyList<Column> columns)
    {
        if (!columns.Any(c => c.IsPrimary))
        {
            throw new InvalidOperationException($"Missing primary key columns for table {sourceTableName}.");
        }
    }

    private static string EscapeLiteral(string value)
        => value.Replace("'", "''", StringComparison.Ordinal);
}
