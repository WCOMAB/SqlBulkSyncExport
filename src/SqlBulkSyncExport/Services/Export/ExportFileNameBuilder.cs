using SqlBulkSyncExport.Helpers;

namespace SqlBulkSyncExport.Services.Export;

public sealed class ExportFileNameBuilder : IExportFileNameBuilder
{
    private const string VersionFormat = "0000000000";

    public string BuildDeltaFileName(string targetFileName, string stamp, long fromVersion, long toVersion)
        => OutputFileNames.InsertBeforeExtension(
            targetFileName,
            $"{stamp}_{FormatVersion(fromVersion)}_{FormatVersion(toVersion)}");

    public string BuildDeletedFileName(string deletedFileName, string stamp, long fromVersion, long toVersion)
        => OutputFileNames.InsertBeforeExtension(
            deletedFileName,
            $"{stamp}_{FormatVersion(fromVersion)}_{FormatVersion(toVersion)}");

    public string BuildFullFileName(string targetFileName, string stamp, long toVersion)
        => OutputFileNames.InsertBeforeExtension(
            targetFileName,
            $"{stamp}_{FormatVersion(toVersion)}_full");

    private static string FormatVersion(long version)
        => version.ToString(VersionFormat);
}
