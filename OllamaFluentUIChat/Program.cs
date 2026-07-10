using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Microsoft.FluentUI.AspNetCore.Components;
using OllamaFluentUIChat.Components;
using OllamaFluentUIChat.Services.Helpers;
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

    string dbPath = Path.Combine(builder.Environment.ContentRootPath, "ollama_benchmark.db");

    // 2. Configuração do Serilog
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console() 
        .WriteTo.SQLite(
            sqliteDbPath: dbPath, 
            tableName: "Logs",
            batchSize: 1
        ));

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddFluentUIComponents();
    builder.Services.AddScoped(sp => new HttpClient());
    builder.Services.AddTransient<IDapperContext, DapperContext>();
    builder.Services.AddTransient<IOllamaGpuService, OllamaGpuService>();

    builder.Services.AddHttpClient<InternetConnectivityService>();

    builder.Services.AddSingleton<MarkdownRenderer>();
    builder.Services.AddHttpClient<ReadMeService>();

    builder.Services.AddScoped<IBenchmarkRepository, BenchmarkRepository>();
    builder.Services.AddScoped<ILogRepository, LogRepository>();

    builder.Services.AddTransient<ITranslationService, TranslationService>();

    var app = builder.Build();

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