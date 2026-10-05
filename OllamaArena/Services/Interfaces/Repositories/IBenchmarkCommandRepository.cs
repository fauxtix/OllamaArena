using OllamaArena.Models.Entities;
using System.Data;

namespace OllamaArena.Services.Interfaces.Repositories;

/// <summary>
/// Comandos de benchmarks: escrita em <c>Prompts</c> e <c>Respostas</c> e as suas
/// eliminações. Separado das projeções (<see cref="IBenchmarkQueryRepository"/>) e
/// das agregações (<see cref="IBenchmarkStatsRepository"/>) para que quem só escreve
/// não dependa delas.
/// </summary>
public interface IBenchmarkCommandRepository
{
    Task<int> CreatePromptAsync(string textoPrompt, double temperatura, string descricao);

    /// <summary>
    /// Procura (ou cria) o prompt pelo texto e devolve o id + a descrição guardada.
    /// <paramref name="connection"/>/<paramref name="transaction"/> são opcionais e
    /// permitem participar numa transacção aberta pelo chamador (ver <c>ChatTurnWriter</c>).
    /// </summary>
    Task<(int PromptId, string Descricao)> GetOrCreatePromptIdAsync(string textoPrompt, string descricao,
        IDbConnection? connection = null, IDbTransaction? transaction = null);

    /// <summary>
    /// Insere a resposta de benchmark. Devolve <c>false</c> (em vez de lançar) para que o
    /// <c>ChatTurnWriter</c> possa reverter a transacção inteira quando a escrita falha.
    /// </summary>
    Task<bool> CreateResponseAsync(BenchmarkResponse response,
        IDbConnection? connection = null, IDbTransaction? transaction = null);

    Task<bool> UpdatePromptAsync(int id, string newText);
    Task<bool> UpdateResponseTextAsync(int responseId, string newText);
    Task<bool> UpdateResponseEvaluationAsync(BenchmarkResponse res);

    /// <summary>
    /// Apaga um prompt e, por <c>ON DELETE CASCADE</c>, todas as respostas suas.
    /// </summary>
    Task<bool> DeletePromptAsync(int promptId);

    /// <summary>
    /// Apaga a resposta indicada e, se ficar a ser a última do prompt, também o prompt.
    /// </summary>
    Task<bool> DeleteSpecificResponseAsync(int responseId);

    /// <summary>
    /// Apaga as respostas indicadas e os prompts que ficarem sem nenhuma (transacção única).
    /// </summary>
    Task<int> DeleteResponsesAsync(IEnumerable<int> responseIds);

    /// <summary>
    /// Apaga todos os prompts e, por <c>ON DELETE CASCADE</c>, todas as respostas.
    /// </summary>
    Task<bool> DeleteAllPromptsAsync();
}