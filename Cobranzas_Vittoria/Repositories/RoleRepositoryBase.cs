using System.Data;
using Cobranzas_Vittoria.Data;
using Dapper;

namespace Cobranzas_Vittoria.Repositories
{
    public abstract class RepositoryBase
    {
        protected readonly IDbConnectionFactory _factory;
        protected RepositoryBase(IDbConnectionFactory factory) => _factory = factory;
        protected IDbConnection Open()
        {
            var db = _factory.CreateConnection();
            if (db.State != ConnectionState.Open)
                db.Open();
            return db;
        }
    }
}
