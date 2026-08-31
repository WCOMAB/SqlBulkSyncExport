using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;

namespace SqlBulkSyncExport.Services.Sql;

public sealed class SqlConnectionFactory(ILogger<SqlConnectionFactory> logger) : ISqlConnectionFactory
{
    private static readonly string[] AzureSqlScopes = ["https://database.windows.net/.default"];

    public async Task<SqlConnection> CreateOpenConnectionAsync(
        SourceConfig source,
        bool applicationIntentReadOnly,
        CancellationToken cancellationToken)
    {
        var builder = new SqlConnectionStringBuilder(source.ConnectionString);
        if (applicationIntentReadOnly)
        {
            builder.ApplicationIntent = ApplicationIntent.ReadOnly;
        }

        var connection = new SqlConnection(builder.ConnectionString);

        if (source.EntraIdAuth)
        {
            var options = new DefaultAzureCredentialOptions();
            if (!string.IsNullOrWhiteSpace(source.TenantId))
            {
                options.TenantId = source.TenantId;
            }
            else
            {
                var envTenant = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
                if (!string.IsNullOrWhiteSpace(envTenant))
                {
                    options.TenantId = envTenant;
                }
            }

            var credential = new DefaultAzureCredential(options);
            logger.LogInformation("Acquiring Entra ID token for Azure SQL...");
            var token = await credential.GetTokenAsync(
                new TokenRequestContext(AzureSqlScopes),
                cancellationToken);
            connection.AccessToken = token.Token;
        }

        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
