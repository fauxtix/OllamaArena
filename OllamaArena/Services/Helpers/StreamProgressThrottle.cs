namespace OllamaArena.Services.Helpers;

/// <summary>
/// Porta de entrada para "publicar progresso" durante o streaming, com limitação de
/// frequência. Existe para que a decisão de renderizar (custo de UI + interop JS) possa ser
/// testada sem browser: a implementação anterior re-renderizava e fazia uma chamada JS a
/// cada token, o que com modelos rápidos chega a milhares de renders por resposta.
///
/// Sem limitação, o primeiro progresso passa sempre (o utilizador vê feedback imediato) e
/// os seguintes só depois de <c>interval</c> ter passado.
/// </summary>
public sealed class StreamProgressThrottle
{
    private readonly double _intervalMs;
    private readonly Func<double> _clock;
    private double _lastPublishMs;
    private bool _publishedAny;

    public StreamProgressThrottle(TimeSpan interval, Func<double>? clock = null)
    {
        if (interval < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), "O intervalo não pode ser negativo.");

        _intervalMs = interval.TotalMilliseconds;
        _clock = clock ?? (() => Environment.TickCount64);
    }

    /// <summary>Devolve <c>true</c> quando o caller deve publicar o progresso agora.</summary>
    public bool ShouldPublish()
    {
        double now = _clock();

        if (!_publishedAny || now - _lastPublishMs >= _intervalMs)
        {
            _publishedAny = true;
            _lastPublishMs = now;
            return true;
        }

        return false;
    }
}