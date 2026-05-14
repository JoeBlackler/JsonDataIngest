using Microsoft.Data.SqlClient;

namespace JsonIngestService.DataAccess;

public sealed class DbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public DbConnectionFactory(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public SqlConnection CreateConnection() => new(_connectionString);
}
