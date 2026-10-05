using OllamaArena.Services.Interfaces.Services;
using System.Data;

namespace OllamaArena.Services.Implementations.Repositories;

/// <summary>
/// Executa uma operação de repositório numa ligação/transacção partilhada quando o chamador
/// as fornece, ou numa ligação própria (que então é fechada aqui) quando não.
///
/// É o que permite a <c>ChatTurnWriter</c> gravar conversa + duas mensagens + resposta de
/// benchmark numa única transacção atómica, sem duplicar o SQL dos repositórios.
/// </summary>
public static class RepositoryScope
{
    public static async Task<T> RunAsync<T>(
        IDapperContext context,
        IDbConnection? connection,
        IDbTransaction? transaction,
        Func<IDbConnection, IDbTransaction?, Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(operation);

        if (connection is not null)
            return await operation(connection, transaction);

        using var own = context.CreateConnection();
        return await operation(own, null);
    }
}
