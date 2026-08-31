using SqlBulkSyncExport.Helpers;
using SqlBulkSyncExport.Models.Schema;

namespace SqlBulkSyncExport.Tests;

public sealed class SqlStatementsTests
{
    private static readonly Column[] Columns =
    [
        new("Id", "[Id]", "int", IsIdentity: true, IsPrimary: true, IsNullable: false, Collation: null),
        new("Name", "[Name]", "nvarchar(100)", IsIdentity: false, IsPrimary: false, IsNullable: true, Collation: "SQL_Latin1_General_CP1_CI_AS")
    ];

    [Fact]
    public Task NewOrUpdatedSelect_MatchesSnapshot()
        => Verify(SqlStatements.GetNewOrUpdatedSelectStatement("dbo.Customers", Columns));

    [Fact]
    public Task DeletedSelect_MatchesSnapshot()
        => Verify(SqlStatements.GetDeletedPrimaryKeysSelectStatement("dbo.Customers", Columns));

    [Fact]
    public Task SelectAll_MatchesSnapshot()
        => Verify(SqlStatements.GetSelectAllStatement("dbo.Customers", Columns, useSnapshotIsolation: false));

    [Fact]
    public Task ChangeTrackingVersion_MatchesSnapshot()
        => Verify(SqlStatements.GetChangeTrackingVersionStatement("dbo.Customers"));
}
