using OllamaArena.Services.Helpers;

namespace OllamaArena.Tests;

/// <summary>
/// Testes da limitação de frequência do progresso durante o streaming. O relógio é
/// injectado para tornar o comportamento determinístico, sem <c>Thread.Sleep</c>.
/// </summary>
public class StreamProgressThrottleTests
{
    [Fact]
    public void ShouldPublish_PrimeiroProgressoPassaSempre()
    {
        double now = 1000;
        var throttle = new StreamProgressThrottle(TimeSpan.FromMilliseconds(80), () => now);

        Assert.True(throttle.ShouldPublish());
    }

    [Fact]
    public void ShouldPublish_BloqueiaProgressosDentroDoIntervalo()
    {
        double now = 1000;
        var throttle = new StreamProgressThrottle(TimeSpan.FromMilliseconds(80), () => now);

        Assert.True(throttle.ShouldPublish());

        now += 10;
        Assert.False(throttle.ShouldPublish());

        now += 20;
        Assert.False(throttle.ShouldPublish());
    }

    [Fact]
    public void ShouldPublish_DeixaPassarAssimQueOIntervaloPassa()
    {
        double now = 1000;
        var throttle = new StreamProgressThrottle(TimeSpan.FromMilliseconds(80), () => now);

        Assert.True(throttle.ShouldPublish());

        now += 79;
        Assert.False(throttle.ShouldPublish());

        now += 1;
        Assert.True(throttle.ShouldPublish());
    }

    [Fact]
    public void ShouldPublish_ComIntervaloZero_PassaEmTodasAsChamadas()
    {
        double now = 1000;
        var throttle = new StreamProgressThrottle(TimeSpan.Zero, () => now);

        Assert.True(throttle.ShouldPublish());
        Assert.True(throttle.ShouldPublish());
        Assert.True(throttle.ShouldPublish());
    }

    [Fact]
    public void Ctor_IntervaloNegativo_LançaArgumentOutOfRange()
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => new StreamProgressThrottle(TimeSpan.FromMilliseconds(-1)));
}