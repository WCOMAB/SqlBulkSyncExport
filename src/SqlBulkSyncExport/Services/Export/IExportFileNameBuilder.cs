namespace SqlBulkSyncExport.Services.Export;

public interface IExportFileNameBuilder
{
    string BuildDeltaFileName(string targetFileName, string stamp, long fromVersion, long toVersion);

    string BuildDeletedFileName(string deletedFileName, string stamp, long fromVersion, long toVersion);

    string BuildFullFileName(string targetFileName, string stamp, long toVersion);
}
