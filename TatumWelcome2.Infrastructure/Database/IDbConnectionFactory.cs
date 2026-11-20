using Microsoft.Data.SqlClient;

namespace TatumWelcome2.Infrastructure.Database;

public interface IDbConnectionFactory
{
    /// <summary>Returns a new, unopened connection. Callers own and dispose it.</summary>
    SqlConnection CreateConnection();
}
