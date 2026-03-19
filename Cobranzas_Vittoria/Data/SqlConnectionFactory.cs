using System.Data;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Data
{
    public class SqlConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;

        public SqlConnectionFactory(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("Default")
                ?? throw new InvalidOperationException("Falta ConnectionStrings:Default");
        }

        public IDbConnection CreateConnection()
        {
            var cn = new SqlConnection(_connectionString);
            if (cn.State != System.Data.ConnectionState.Open)
                cn.Open();
            return cn;
        }
    }
}
