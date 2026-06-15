using System.Data;
using Microsoft.Data.SqlClient;
using OllamaFluentUIChat.Services.Interfaces.Services;

namespace OllamaFluentUIChat.Services.Implementations.Services
{
    public class DapperContext : IDapperContext
    {
        private readonly IConfiguration _configuration;
        private readonly string? _connectionString;
        public DapperContext(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("SqlServerCon");
        }

        public void Execute(Action<IDbConnection> @event)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                @event(connection);
            }
        }
        public IDbConnection CreateConnection() => new SqlConnection(_connectionString);
    }
}
