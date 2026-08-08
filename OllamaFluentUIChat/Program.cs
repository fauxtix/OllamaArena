using Microsoft.AspNetCore.Localization;
using Microsoft.FluentUI.AspNetCore.Components;
using OllamaFluentUIChat.Components;
using OllamaFluentUIChat.PromptTemplates;
using OllamaFluentUIChat.Services;
using OllamaFluentUIChat.Services.Helpers;
using OllamaFluentUIChat.Services.Implementations.Repositories;
using OllamaFluentUIChat.Services.Implementations.Services;
using OllamaFluentUIChat.Services.Interfaces.Repositories;
using OllamaFluentUIChat.Services.Interfaces.Services;
using OllamaFluentUIChat.Services.Providers;
using Serilog;

// 1. Logger inicial para capturar o terminal
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Ficheiro local não versionado para chaves de API (appsettings.Local.json no .gitignore)
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

    // Localização
    builder.Services.AddLocalization(); 

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

    builder.Services.AddControllers();

    builder.Services.AddFluentUIComponents();

    builder.Services.AddTransient<IDapperContext, DapperContext>();
    builder.Services.AddTransient<IOllamaGpuService, OllamaGpuService>();

    builder.Services.AddTransient<ISystemPromptService, SystemPromptService>();
    builder.Services.AddTransient<IPromptTemplateProvider, PromptTemplateProvider>();
    builder.Services.AddTransient<PromptFilesService>();
    builder.Services.AddTransient<EvaluatePromptTemplate>();

    builder.Services.AddHttpClient<IAnalysisService, LocalAnalysisService>();
    builder.Services.AddHttpClient<InternetConnectivityService>();

    builder.Services.AddSingleton<MarkdownRenderer>();
    builder.Services.AddHttpClient<ReadMeService>();
    builder.Services.AddTransient<JudgesFeedbackService>();

    // Clientes nomeados para os juízes automáticos (Gemini e OpenRouter)
    builder.Services.AddHttpClient("Gemini", client => client.Timeout = TimeSpan.FromSeconds(30));
    builder.Services.AddHttpClient("OpenRouter", client => client.Timeout = TimeSpan.FromSeconds(60));
    builder.Services.AddScoped<IBenchmarkRepository, BenchmarkRepository>();
    builder.Services.AddScoped<ILogRepository, LogRepository>();

    builder.Services.AddHttpClient<ITranslationService, TranslationService>(client =>
    {
        client.BaseAddress = new Uri("http://localhost:11434");
        client.Timeout = TimeSpan.FromMinutes(4);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    });

    // Cliente nomeado para o streaming do chat: timeout alargado porque o Ollama
    // pode demorar muito tempo antes de devolver o primeiro byte (carregamento + KV cache).
    builder.Services.AddHttpClient("Ollama", client =>
    {
        client.BaseAddress = new Uri("http://localhost:11434");
        client.Timeout = TimeSpan.FromMinutes(10);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    });

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<IDapperContext>();
        DatabaseSchemaInitializer.EnsureRecommendationColumns(context);
    }

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    // ---------- LOCALIZAÇÃO ----------
    string[] supportedCultures = ["pt", "en"];

    var localizationOptions = new RequestLocalizationOptions()
        .SetDefaultCulture("pt")
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);

    localizationOptions.RequestCultureProviders.Clear();
    localizationOptions.RequestCultureProviders.Add(new CookieRequestCultureProvider());

    app.UseRequestLocalization(localizationOptions);
    app.UseRouting();
    // ---------------------------------

    app.MapGet("/culture-reload", (string redirectUri) =>
    {
        return Results.Content($@"
        <html><body>
        <script>location.replace('{redirectUri}');</script>
        </body></html>", "text/html");
    });
    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    app.UseHttpsRedirection();
    app.UseAntiforgery();
    app.MapStaticAssets();

    app.MapControllers();

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