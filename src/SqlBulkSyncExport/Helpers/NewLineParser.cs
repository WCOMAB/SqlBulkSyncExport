namespace SqlBulkSyncExport.Helpers;

public static class NewLineParser
{
    public static string Parse(string? value)
    {
        if (value is null)
        {
            return "\r\n";
        }

        // Preserve intentional newline strings before whitespace checks.
        if (value is "\r\n" or "\n" or "\r")
        {
            return value;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return "\r\n";
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "crlf" or "\\r\\n" => "\r\n",
            "lf" or "\\n" => "\n",
            "cr" or "\\r" => "\r",
            _ => value
        };
    }
}
