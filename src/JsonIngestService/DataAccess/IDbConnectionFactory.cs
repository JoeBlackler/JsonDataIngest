using Microsoft.Data.SqlClient;

namespace JsonIngestService.DataAccess;

public interface IDbConnectionFactory
{
    SqlConnection CreateConnection();
}
