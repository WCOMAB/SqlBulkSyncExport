namespace SqlBulkSyncExport.Services.Export;

public static class ExportDecisionMaker
{
    public static ExportDecision DecideChangeTracking(
        TableSyncState? state,
        TableVersion sourceVersion,
        bool seed)
    {
        var last = state?.CurrentVersion ?? -1;
        var fromVersion = last < 0 ? 0 : last;

        if (seed || last < 0 || last < sourceVersion.MinValidVersion)
        {
            return new ExportDecision(ExportMode.Full, fromVersion, sourceVersion.CurrentVersion, IsFullSyncMode: false);
        }

        if (last == sourceVersion.CurrentVersion)
        {
            return new ExportDecision(ExportMode.Skip, fromVersion, sourceVersion.CurrentVersion, IsFullSyncMode: false);
        }

        return new ExportDecision(ExportMode.Delta, fromVersion, sourceVersion.CurrentVersion, IsFullSyncMode: false);
    }

    public static ExportDecision DecideFullSync(
        TableSyncState? state,
        TableVersion sourceVersion,
        long intervalMilliseconds)
    {
        var last = state?.CurrentVersion ?? -1;
        if (last < 0 || sourceVersion.CurrentVersion - last >= intervalMilliseconds)
        {
            return new ExportDecision(
                ExportMode.Full,
                FromVersion: last < 0 ? 0 : last,
                ToVersion: sourceVersion.CurrentVersion,
                IsFullSyncMode: true);
        }

        return new ExportDecision(
            ExportMode.Skip,
            FromVersion: last,
            ToVersion: sourceVersion.CurrentVersion,
            IsFullSyncMode: true);
    }
}
