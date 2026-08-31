namespace SqlBulkSyncExport.Helpers;

public static class OutputFileNames
{
    public static string ValidateFileName(string? value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        var trimmed = value.Trim();
        if (trimmed.Contains('/') || trimmed.Contains('\\') || trimmed.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"{parameterName} must be a file name without path segments: '{value}'.",
                parameterName);
        }

        if (string.IsNullOrEmpty(Path.GetExtension(trimmed)))
        {
            throw new ArgumentException(
                $"{parameterName} must include a file extension: '{value}'.",
                parameterName);
        }

        if (string.IsNullOrEmpty(Path.GetFileNameWithoutExtension(trimmed)))
        {
            throw new ArgumentException(
                $"{parameterName} must include a file name before the extension: '{value}'.",
                parameterName);
        }

        return trimmed;
    }

    public static string DefaultTargetFile(string tableKey)
        => $"{tableKey}.csv";

    public static string DefaultDeletedFile(string targetFileName)
    {
        var stem = Path.GetFileNameWithoutExtension(targetFileName);
        var ext = Path.GetExtension(targetFileName);
        return $"{stem}.deleted{ext}";
    }

    /// <summary>Inserts <paramref name="token"/> before the file extension.</summary>
    public static string InsertBeforeExtension(string fileName, string token)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        return $"{stem}_{token}{ext}";
    }
}
