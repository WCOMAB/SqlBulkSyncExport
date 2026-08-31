namespace SqlBulkSyncExport.Services.Sql;

public static class SourceTimeZoneResolver
{
    public static TimeZoneInfo Resolve(string timeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException ex)
        {
            throw new InvalidOperationException(
                $"Source timezone id '{timeZoneId}' from CURRENT_TIMEZONE_ID() is not available on this host.",
                ex);
        }
        catch (InvalidTimeZoneException ex)
        {
            throw new InvalidOperationException(
                $"Source timezone id '{timeZoneId}' from CURRENT_TIMEZONE_ID() is invalid on this host.",
                ex);
        }
    }
}
