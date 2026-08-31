using SqlBulkSyncExport.Models;
using SqlBulkSyncExport.Models.Schema;
using SqlBulkSyncExport.Models.State;
using SqlBulkSyncExport.Services.Export;

namespace SqlBulkSyncExport.Tests;

public sealed class ExportFileNameBuilderTests
{
    private readonly ExportFileNameBuilder _sut = new();

    [Fact]
    public void BuildDeltaAndDeleted_InsertBeforeExtension()
    {
        Assert.Equal(
            "customers_export_20260101120000_0000000010_0000000020.csv",
            _sut.BuildDeltaFileName("customers_export.csv", "20260101120000", 10, 20));

        Assert.Equal(
            "customers_deleted_20260101120000_0000000010_0000000020.csv",
            _sut.BuildDeletedFileName("customers_deleted.csv", "20260101120000", 10, 20));
    }

    [Fact]
    public void BuildFull_InsertsFullToken()
    {
        Assert.Equal(
            "customers_export_20260101120000_0000000020_full.csv",
            _sut.BuildFullFileName("customers_export.csv", "20260101120000", 20));
    }
}

public sealed class ExportDecisionMakerTests
{
    [Fact]
    public void ChangeTracking_NeverSynced_IsFull()
    {
        var decision = ExportDecisionMaker.DecideChangeTracking(
            state: null,
            new TableVersion("dbo.T", 50, 1, DateTimeOffset.UtcNow),
            seed: false);

        Assert.Equal(ExportMode.Full, decision.Mode);
        Assert.Equal(50, decision.ToVersion);
    }

    [Fact]
    public void ChangeTracking_InvalidWatermark_IsFull()
    {
        var decision = ExportDecisionMaker.DecideChangeTracking(
            new TableSyncState { CurrentVersion = 5 },
            new TableVersion("dbo.T", 50, 10, DateTimeOffset.UtcNow),
            seed: false);

        Assert.Equal(ExportMode.Full, decision.Mode);
    }

    [Fact]
    public void ChangeTracking_SameVersion_IsSkip()
    {
        var decision = ExportDecisionMaker.DecideChangeTracking(
            new TableSyncState { CurrentVersion = 50 },
            new TableVersion("dbo.T", 50, 1, DateTimeOffset.UtcNow),
            seed: false);

        Assert.Equal(ExportMode.Skip, decision.Mode);
    }

    [Fact]
    public void ChangeTracking_Newer_IsDelta()
    {
        var decision = ExportDecisionMaker.DecideChangeTracking(
            new TableSyncState { CurrentVersion = 40 },
            new TableVersion("dbo.T", 50, 1, DateTimeOffset.UtcNow),
            seed: false);

        Assert.Equal(ExportMode.Delta, decision.Mode);
        Assert.Equal(40, decision.FromVersion);
        Assert.Equal(50, decision.ToVersion);
    }

    [Fact]
    public void ChangeTracking_Seed_IsFull()
    {
        var decision = ExportDecisionMaker.DecideChangeTracking(
            new TableSyncState { CurrentVersion = 40 },
            new TableVersion("dbo.T", 50, 1, DateTimeOffset.UtcNow),
            seed: true);

        Assert.Equal(ExportMode.Full, decision.Mode);
    }

    [Fact]
    public void FullSync_IntervalNotElapsed_IsSkip()
    {
        var decision = ExportDecisionMaker.DecideFullSync(
            new TableSyncState { CurrentVersion = 1_000 },
            new TableVersion("dbo.T", 1_500, 0, DateTimeOffset.UtcNow),
            intervalMilliseconds: 1_000);

        Assert.Equal(ExportMode.Skip, decision.Mode);
    }

    [Fact]
    public void FullSync_IntervalElapsed_IsFull()
    {
        var decision = ExportDecisionMaker.DecideFullSync(
            new TableSyncState { CurrentVersion = 1_000 },
            new TableVersion("dbo.T", 2_500, 0, DateTimeOffset.UtcNow),
            intervalMilliseconds: 1_000);

        Assert.Equal(ExportMode.Full, decision.Mode);
        Assert.True(decision.IsFullSyncMode);
    }
}
