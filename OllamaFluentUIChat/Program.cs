using Microsoft.FluentUI.AspNetCore.Components;
using OllamaFluentUIChat.Components;
using OllamaFluentUIChat.Services.Implementations.Repositories;
using OllamaFluentUIChat.Services.Implementations.Services;
using OllamaFluentUIChat.Services.Interfaces.Repositories;
using OllamaFluentUIChat.Services.Interfaces.Services;
using Serilog;

// 1. Logger inicial para capturar o terminal
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // DETERMINAR O CAMINHO ABSOLUTO PARA A BASE DE DADOS
    // Isto força o Serilog a gravar na raiz do projeto, que é onde o teu Dapper costuma ler em Development
    string dbPath = Path.Combine(builder.Environment.ContentRootPath, "ollama_benchmark.db");

    // 2. Configuração do Serilog
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console() // Mantém o Console para veres os erros no terminal se a BD falhar
        .WriteTo.SQLite(
            sqliteDbPath: dbPath, // Usa o caminho absoluto aqui
            tableName: "Logs",
            batchSize: 1
        ));

    // Add services to the container.
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddFluentUIComponents();
    builder.Services.AddScoped(sp => new HttpClient());
    builder.Services.AddTransient<IDapperContext, DapperContext>();
    builder.Services.AddTransient<IOllamaGpuService, OllamaGpuService>();
    builder.Services.AddScoped<IBenchmarkRepository, BenchmarkRepository>();
    builder.Services.AddScoped<ILogRepository, LogRepository>();

    var app = builder.Build();

    // ESTE LOG TEM DE APARECER
    Log.Information("Ollama FluentUI Chat iniciado com sucesso no .NET 10.");

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    app.UseHttpsRedirection();
    app.UseAntiforgery();
    app.MapStaticAssets();

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "A aplicação falhou inesperadamente durante o arranque.");
}
finally
{
    Log.CloseAndFlush();
}