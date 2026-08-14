using Dapper;
using Microsoft.Data.Sqlite;
using OllamaArena.Services.Helpers;

namespace OllamaArena.Tests;

public class SqliteTypeHandlersTests
{
    public class Modelo
    {
        public float? GeminiRating { get; set; }
        public float? OpenRouterRating { get; set; }
        public double TokensPorSegundo { get; set; }
        public double? TempoPuroMs { get; set; }
    }

    public SqliteTypeHandlersTests()
    {
        SqliteTypeHandlers.Register();
    }

    [Fact]
    public void ValoresInteirosNoSqlite_ConverteParaDoubleEFloat()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        conn.Execute("CREATE TABLE Respostas (Id INTEGER, GeminiRating NUMERIC, OpenRouterRating NUMERIC, TokensPorSegundo REAL, TempoPuroMs REAL)");
        conn.Execute("INSERT INTO Respostas (Id, GeminiRating, OpenRouterRating, TokensPorSegundo, TempoPuroMs) VALUES (1, 4, 5, 4, 1500)");

        var linha = conn.QueryFirst<Modelo>("SELECT GeminiRating, OpenRouterRating, TokensPorSegundo, TempoPuroMs FROM Respostas WHERE Id = 1");

        Assert.Equal(4.0f, linha.GeminiRating);
        Assert.Equal(5.0f, linha.OpenRouterRating);
        Assert.Equal(4.0, linha.TokensPorSegundo);
        Assert.Equal(1500.0, linha.TempoPuroMs);
    }

    [Fact]
    public void ValoresNulos_DevolveNull()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        conn.Execute("CREATE TABLE Respostas (Id INTEGER, GeminiRating NUMERIC, TempoPuroMs REAL)");
        conn.Execute("INSERT INTO Respostas (Id, GeminiRating, TempoPuroMs) VALUES (1, NULL, NULL)");

        var linha = conn.QueryFirst<Modelo>("SELECT GeminiRating, TempoPuroMs FROM Respostas WHERE Id = 1");

        Assert.Null(linha.GeminiRating);
        Assert.Null(linha.TempoPuroMs);
    }

    [Fact]
    public void ValoresReais_ConverteSemPerda()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        conn.Execute("CREATE TABLE Respostas (Id INTEGER, GeminiRating NUMERIC, TokensPorSegundo REAL)");
        conn.Execute("INSERT INTO Respostas (Id, GeminiRating, TokensPorSegundo) VALUES (1, 4.5, 12.75)");

        var linha = conn.QueryFirst<Modelo>("SELECT GeminiRating, TokensPorSegundo FROM Respostas WHERE Id = 1");

        Assert.Equal(4.5f, linha.GeminiRating);
        Assert.Equal(12.75, linha.TokensPorSegundo);
    }
}
