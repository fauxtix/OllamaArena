using Dapper;
using OllamaFluentUIChat.Models.DTO;
using OllamaFluentUIChat.Models.Entities;
using OllamaFluentUIChat.Services.Interfaces.Repositories;
using OllamaFluentUIChat.Services.Interfaces.Services;

namespace OllamaFluentUIChat.Services.Implementations.Repositories;

public class ConversationRepository : IConversationRepository
{
    private readonly IDapperContext _context;
    private readonly ILogger<ConversationRepository> _logger;

    public ConversationRepository(IDapperContext context, ILogger<ConversationRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> CreateConversationAsync(string titulo, string nomeModelo)
    {
        var sql = @"
                INSERT INTO Conversas (Titulo, NomeModelo, DataCriacao, DataUltimaAtividade)
                VALUES (@Titulo, @NomeModelo, @DataCriacao, @DataUltimaAtividade);
                SELECT last_insert_rowid();";

        try
        {
            using var connection = _context.CreateConnection();
            var now = DateTime.UtcNow.ToString("o");
            return await connection.ExecuteScalarAsync<int>(sql, new
            {
                Titulo = titulo,
                NomeModelo = nomeModelo,
                DataCriacao = now,
                DataUltimaAtividade = now
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao criar conversa");
            return 0;
        }
    }

    public async Task<bool> AddMessageAsync(ChatConversationMessage message)
    {
        var sql = @"
                INSERT INTO ConversaMensagens (ConversationId, Role, Content, Reasoning, Temperature, ElapsedTime, Timestamp)
                VALUES (@ConversationId, @Role, @Content, @Reasoning, @Temperature, @ElapsedTime, @Timestamp);";

        try
        {
            using var connection = _context.CreateConnection();
            await connection.ExecuteAsync(sql, new
            {
                message.ConversationId,
                message.Role,
                message.Content,
                message.Reasoning,
                message.Temperature,
                message.ElapsedTime,
                Timestamp = message.Timestamp.ToString("o")
            });
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao gravar mensagem da conversa {ConversationId}", message.ConversationId);
            return false;
        }
    }

    public async Task<bool> TouchConversationAsync(int conversationId)
    {
        var sql = "UPDATE Conversas SET DataUltimaAtividade = @DataUltimaAtividade WHERE Id = @Id;";
        try
        {
            using var connection = _context.CreateConnection();
            int affected = await connection.ExecuteAsync(sql, new
            {
                Id = conversationId,
                DataUltimaAtividade = DateTime.UtcNow.ToString("o")
            });
            return affected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao atualizar atividade da conversa {ConversationId}", conversationId);
            return false;
        }
    }

    public async Task<List<ChatConversation>> GetConversationsAsync()
    {
        var sql = @"
                SELECT Id, Titulo, NomeModelo, DataCriacao, DataUltimaAtividade
                FROM Conversas
                ORDER BY DataUltimaAtividade DESC;";

        try
        {
            using var connection = _context.CreateConnection();
            var result = await connection.QueryAsync<ChatConversation>(sql);
            return [.. result];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao listar conversas");
            return [];
        }
    }

    public async Task<List<ChatConversationMessage>> GetMessagesAsync(int conversationId)
    {
        var sql = @"
                SELECT Id, ConversationId, Role, Content, Reasoning, Temperature, ElapsedTime, Timestamp
                FROM ConversaMensagens
                WHERE ConversationId = @ConversationId
                ORDER BY Id ASC;";

        try
        {
            using var connection = _context.CreateConnection();
            var result = await connection.QueryAsync<ChatConversationMessage>(sql, new { ConversationId = conversationId });
            return [.. result];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao carregar mensagens da conversa {ConversationId}", conversationId);
            return [];
        }
    }

    public async Task<List<ConversationExportItem>> GetAllForExportAsync()
    {
        var resultado = new List<ConversationExportItem>();
        var sqlConversas = @"
                SELECT Id, Titulo, NomeModelo, DataCriacao, DataUltimaAtividade
                FROM Conversas
                ORDER BY DataCriacao ASC;";

        try
        {
            using var connection = _context.CreateConnection();
            var conversas = await connection.QueryAsync<ChatConversation>(sqlConversas);

            foreach (var conversa in conversas)
            {
                var mensagens = await GetMessagesAsync(conversa.Id);
                resultado.Add(new ConversationExportItem
                {
                    Titulo = conversa.Titulo,
                    NomeModelo = conversa.NomeModelo,
                    DataCriacao = conversa.DataCriacao,
                    DataUltimaAtividade = conversa.DataUltimaAtividade,
                    Mensagens = mensagens
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao exportar conversas");
        }

        return resultado;
    }

    public async Task<(int Conversas, int Mensagens)> ImportAsync(List<ConversationExportItem> itens)
    {
        int contagemConversas = 0;
        int contagemMensagens = 0;

        if (itens is null || itens.Count == 0) return (0, 0);

        try
        {
            using var connection = _context.CreateConnection();
            using var transaction = connection.BeginTransaction();

            foreach (var item in itens)
            {
                var sqlConversa = @"
                        INSERT INTO Conversas (Titulo, NomeModelo, DataCriacao, DataUltimaAtividade)
                        VALUES (@Titulo, @NomeModelo, @DataCriacao, @DataUltimaAtividade);
                        SELECT last_insert_rowid();";

                int novoId = await connection.ExecuteScalarAsync<int>(sqlConversa, new
                {
                    Titulo = item.Titulo,
                    NomeModelo = item.NomeModelo,
                    DataCriacao = item.DataCriacao.ToString("o"),
                    DataUltimaAtividade = item.DataUltimaAtividade.ToString("o")
                }, transaction);

                if (novoId == 0) continue;
                contagemConversas++;

                var sqlMensagem = @"
                        INSERT INTO ConversaMensagens (ConversationId, Role, Content, Reasoning, Temperature, ElapsedTime, Timestamp)
                        VALUES (@ConversationId, @Role, @Content, @Reasoning, @Temperature, @ElapsedTime, @Timestamp);";

                foreach (var msg in item.Mensagens)
                {
                    await connection.ExecuteAsync(sqlMensagem, new
                    {
                        ConversationId = novoId,
                        Role = msg.Role,
                        Content = msg.Content,
                        Reasoning = msg.Reasoning,
                        Temperature = msg.Temperature,
                        ElapsedTime = msg.ElapsedTime,
                        Timestamp = msg.Timestamp.ToString("o")
                    }, transaction);
                    contagemMensagens++;
                }
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao importar conversas");
        }

        return (contagemConversas, contagemMensagens);
    }

    public async Task<bool> DeleteConversationAsync(int conversationId)
    {
        var sql = "DELETE FROM Conversas WHERE Id = @Id; PRAGMA optimize;";
        try
        {
            using var connection = _context.CreateConnection();
            int affected = await connection.ExecuteAsync(sql, new { Id = conversationId });
            return affected > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao apagar conversa {ConversationId}", conversationId);
            return false;
        }
    }
}
