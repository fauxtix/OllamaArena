using System.Data;

namespace Services.Interfaces.Services
{
    public interface IDapperContext
    {
        public IDbConnection CreateConnection();
        public void Execute(Action<IDbConnection> @event);
    }
}
