using System.Data;

namespace OllamaArena.Services.Interfaces.Services
{
    public interface IDapperContext
    {
        public IDbConnection CreateConnection();
        public void Execute(Action<IDbConnection> @event);

    }
}
