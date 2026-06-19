using Dapper;
using OllamaFluentUIChat.Models.Entities;
using OllamaFluentUIChat.Services.Interfaces.Repositories;
using OllamaFluentUIChat.Services.Interfaces.Services;
using System.Text;

namespace OllamaFluentUIChat.Services.Implementations.Repositories
{
    public class BenchmarkRepository : IBenchmarkRepository
    {
        private readonly IDapperContext _context;

        public BenchmarkRepository(IDapperContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Insere um novo prompt e retorna o ID gerado pelo SQLite.
        /// </summary>
        public async Task<int> CreatePromptAsync(string textoPrompt)
        {
            var sql = @"
                INSERT INTO Prompts (TextoPrompt, DataCriacao) 
                VALUES (@TextoPrompt, @DataCriacao);
                SELECT last_insert_rowid();"; // Comando nativo do SQLite para pegar o ID gerado
            using var connection = _context.CreateConnection();

            return await connection.ExecuteScalarAsync<int>(sql, new
            {
                TextoPrompt = textoPrompt,
                DataCriacao = DateTime.UtcNow.ToString("o") // Guarda em formato ISO 8601 legível
            });
        }

        /// <summary>
        /// Insere a resposta e as métricas de um modelo específico associada a um Prompt.
        /// </summary>
        public async Task CreateResponseAsync(BenchmarkResponse response)
        {
            var sql = @"
                INSERT INTO Respostas (PromptId, ModeloNome, TextoResposta, TokensPorSegundo, TempoPuroMs, TempoCargaMs, TamanhoTokens)
                VALUES (@PromptId, @ModeloNome, @TextoResposta, @TokensPorSegundo, @TempoPuroMs, @TempoCargaMs, @TamanhoTokens);";

            using var connection = _context.CreateConnection();
            await connection.ExecuteAsync(sql, response);
        }

        /// <summary>
        /// Retorna a lista de todos os prompts com as suas respetivas respostas (Ideal para a Split View de Comparação).
        /// </summary>
        public async Task<List<BenchmarkPrompt>> GetAllBenchmarksAsync()
        {
            var sql = @"
                SELECT p.*, r.* FROM Prompts p
                LEFT JOIN Respostas r ON p.Id = r.PromptId
                ORDER BY p.Id DESC, r.TokensPorSegundo DESC;"; // Ordena pelos mais recentes e melhores modelos

            var promptDictionary = new Dictionary<int, BenchmarkPrompt>();

            // Técnica Multi-Mapping do Dapper para juntar 1-para-Muitos em C#
            using var connection = _context.CreateConnection();
            await connection.QueryAsync<BenchmarkPrompt, BenchmarkResponse, BenchmarkPrompt>(
                sql,
                (prompt, response) =>
                {
                    if (!promptDictionary.TryGetValue(prompt.Id, out var existingPrompt))
                    {
                        existingPrompt = prompt;
                        promptDictionary.Add(existingPrompt.Id, existingPrompt);
                    }

                    if (response != null)
                    {
                        existingPrompt.Answers.Add(response);
                    }

                    return existingPrompt;
                },
                splitOn: "Id" // Diz ao Dapper que a segunda tabela começa na coluna 'Id' da Resposta
            );

            return promptDictionary.Values.ToList();
        }

        /// <summary>
        /// Procura um único prompt e as suas respostas através do ID.
        /// </summary>
        public async Task<BenchmarkPrompt?> GetBenchmarkByIdAsync(int promptId)
        {
            var sql = @"
                SELECT p.*, r.* FROM Prompts p
                LEFT JOIN Respostas r ON p.Id = r.PromptId
                WHERE p.Id = @Id;";

            BenchmarkPrompt? promptResult = null;

            using var connection = _context.CreateConnection();
            await connection.QueryAsync<BenchmarkPrompt, BenchmarkResponse, BenchmarkPrompt         >(
                sql,
                (prompt, response) =>
                {
                    if (promptResult == null)
                    {
                        promptResult = prompt;
                    }
                    if (response != null)
                    {
                        promptResult.Answers.Add(response);
                    }
                    return prompt;
                },
                new { Id = promptId },
                splitOn: "Id"
            );

            return promptResult;
        }

        /// <summary>
        /// Permite editar o texto de um prompt, caso queiras corrigir alguma nota na UI.
        /// </summary>
        public async Task<bool> UpdatePromptAsync(int id, string novoTexto)
        {
            var sql = "UPDATE Prompts SET TextoPrompt = @TextoPrompt WHERE Id = @Id;";
            using var connection = _context.CreateConnection();
            int AffectedLines = await connection.ExecuteAsync(sql, new { TextoPrompt = novoTexto, Id = id });
            return AffectedLines > 0;
        }

        /// <summary>
        /// Permite atualizar o texto de uma resposta (ex: se quiseres adicionar anotações manuais de qualidade).
        /// </summary>
        public async Task<bool> UpdateResponseTextAsync(int responseId, string newText)
        {
            var sql = "UPDATE Respostas SET TextoResposta = @ResponseText WHERE Id = @Id;";
            using var connection = _context.CreateConnection();
            int AffectedLines = await connection.ExecuteAsync(sql, new { ResponseText = newText, Id = responseId });
            return AffectedLines > 0;
        }

        /// <summary>
        /// Apaga um prompt inteiro. Nota: Como configurámos ON DELETE CASCADE no DB Browser,
        /// todas as respostas associadas a este prompt serão apagadas automaticamente pelo SQLite!
        /// </summary>
        public async Task<bool> DeletePromptAndHistoryAsync(int promptId)
        {
            var sql = "DELETE FROM Prompts WHERE Id = @Id;";
            using var connection = _context.CreateConnection();
            int AffectedLines = await connection.ExecuteAsync(sql, new { Id = promptId });
            return AffectedLines > 0;
        }

        /// <summary>
        /// Apaga apenas a resposta de um modelo específico, sem apagar a pergunta original.
        /// </summary>
        public async Task<bool> DeleteSpecificResponseAsync(int responseId)
        {
            var sql = "DELETE FROM Respostas WHERE Id = @Id;";
            using var connection = _context.CreateConnection();
            int AffectedLines = await connection.ExecuteAsync(sql, new { Id = responseId });
            return AffectedLines > 0;
        }

        public async Task<bool> UpdateResponseEvaluationAsync(BenchmarkResponse res)
        {
            StringBuilder sb = new();
            sb.Append("UPDATE Respostas SET GeminiRating = @GeminiRating, GeminiFeedback = @GeminiFeedback, ");
            sb.Append("ChatGptRating = @ChatGptRating, ChatGptFeedback = @ChatGptFeedback WHERE Id = @Id");
            var sql = sb.ToString();
            using var connection = _context.CreateConnection();
            int AffectedLines = await connection.ExecuteAsync(sql, res);
            return AffectedLines > 0;
        }

    }
}
