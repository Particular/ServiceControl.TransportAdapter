using System.Data.SqlClient;
using System.Threading.Tasks;

namespace ConnectionManager
{
    public class ConnectionFactory
    {
        static string SomeEndpoint = @"Data Source=.\SQLEXPRESS;Initial Catalog=SCAdapter_Some;Integrated Security=True";
        static string OtherEndpoint = @"Data Source=.\SQLEXPRESS;Initial Catalog=SCAdapter_Other;Integrated Security=True";
        static string ServiceControl = @"Data Source=.\SQLEXPRESS;Initial Catalog=SCAdapter;Integrated Security=True";

        public static async Task<SqlConnection> GetConnection(string destination)
        {
            var connectionString = GetConnectionString(destination);

            var connection = new SqlConnection(connectionString);

            await connection.OpenAsync()
                .ConfigureAwait(false);

            return connection;
        }

        static string GetConnectionString(string destination)
        {
            if (destination.StartsWith("SomeEndpoint"))
            {
                return SomeEndpoint;
            }
            if (destination.StartsWith("OtherEndpoint"))
            {
                return OtherEndpoint;
            }
            return ServiceControl;
        }
    }
}
