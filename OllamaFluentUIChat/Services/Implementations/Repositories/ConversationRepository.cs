using Dapper;
using OllamaFluentUIChat.Models.DTO;
using OllamaFluentUIChat.Services.Interfaces.Repositories;
using OllamaFluentUIChat.Services.Interfaces.Services;
using System.Data;

namespace OllamaFluentUIChat.Services.Implementations.Repositories
{
    public class ConversationRepository : IConversationRepository
    {
        private readonly IDapperContext _context;

        public ConversationRepository(IDapperContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Conversation>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            using (var connection = _context.CreateConnection())
            {
                var sproc = "usp_Conversation_GetAll";
                return await connection.QueryAsync<Conversation>(sproc, commandType: CommandType.StoredProcedure);
            }
        }

        public async Task<Conversation?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            using (var connection = _context.CreateConnection())
            {
                var sproc = "usp_Conversation_GetById";
                return await connection.QueryFirstOrDefaultAsync<Conversation>(sproc, new { Id = id }, commandType: CommandType.StoredProcedure);
            }
        }

        public async Task InsertAsync(Conversation conv, CancellationToken cancellationToken = default)
        {
            using (var connection = _context.CreateConnection())
            {
                var sproc = "usp_Conversation_Insert";
                await connection.ExecuteAsync(sproc, new { conv.Id, conv.Title, conv.Json, conv.CreatedAt, conv.UpdatedAt }, commandType: CommandType.StoredProcedure);
            }
        }

        public async Task UpdateAsync(Conversation conv, CancellationToken cancellationToken = default)
        {
            using (var connection = _context.CreateConnection())
            {
                var sproc = "usp_Conversation_Update";
                await connection.ExecuteAsync(sproc, new { conv.Id, conv.Title, conv.Json, conv.UpdatedAt }, commandType: CommandType.StoredProcedure);
            }
        }

        public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
        {
            using (var connection = _context.CreateConnection())
            {
                var sproc = "usp_Conversation_Delete";
                await connection.ExecuteAsync(sproc, new { Id = id }, commandType: CommandType.StoredProcedure);
            }
        }
    }
}
