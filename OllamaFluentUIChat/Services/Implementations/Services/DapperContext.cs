using Microsoft.Data.Sqlite;
using OllamaFluentUIChat.Services.Interfaces.Services;
using System.Data;

namespace OllamaFluentUIChat.Services.Implementations.Services
{
    public class DapperContext : IDapperContext
    {
        private readonly IConfiguration _configuration;
        private readonly string? _connectionString;
        public DapperContext(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("SqliteConnection");
        }

        public void Execute(Action<IDbConnection> @event)
        {
            using (var connection = CreateConnection())
            {
                connection.Open();
                @event(connection);
            }
        }
        public IDbConnection CreateConnection()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // PRAGMAs são por-ligação no SQLite: ativam o ON DELETE CASCADE
            // (definido no esquema) e evitam erros "database is locked" sob concorrência.
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
                command.ExecuteNonQuery();
            }

            return connection;
        }
    }
}
