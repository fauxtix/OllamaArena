using Microsoft.AspNetCore.Localization;
using Microsoft.FluentUI.AspNetCore.Components;
using OllamaArena.Components;
using OllamaArena.PromptTemplates;
using OllamaArena.Services;
using OllamaArena.Services.Helpers;
using OllamaArena.Services.Implementations.Repositories;
using OllamaArena.Services.Implementations.Services;
using OllamaArena.Services.Interfaces.Repositories;
using OllamaArena.Services.Interfaces.Services;
using OllamaArena.Services.Providers;
using Serilog;

// 1. Logger inicial para capturar o terminal
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Ficheiro local não versionado para overrides de configuração
    // (as chaves de API vivem em user-secrets: `dotnet user-secrets set "ApiKeys:Gemini" "<valor>"`)
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

    // Localização
    builder.Services.AddLocalization(); 

    string dbPath = Path.Combine(builder.Environment.ContentRootPath, "ollama_benchmark.db");

    builder.Configuration["ConnectionStrings:SqliteConnection"] = $"Data Source={dbPath}";

    // 2. Configuração do Serilog
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console()
        .WriteTo.SQLite(
            sqliteDbPath: dbPath,
            tableName: "Logs",
            batchSize: 100
        ));

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddControllers();

    builder.Services.AddFluentUIComponents();

    builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection(OllamaOptions.SectionName));
    var ollamaBaseUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";

    builder.Services.AddTransient<IDapperContext, DapperContext>();
    builder.Services.AddTransient<IOllamaGpuService, OllamaGpuService>();

    builder.Services.AddTransient<ISystemPromptService, SystemPromptService>();
    builder.Services.AddTransient<IPromptTemplateProvider, PromptTemplateProvider>();
    builder.Services.AddTransient<PromptFilesService>();
    builder.Services.AddTransient<ChatComposerService>();
    builder.Services.AddTransient<EvaluatePromptTemplate>();

    builder.Services.AddHttpClient<IAnalysisService, LocalAnalysisService>();
    builder.Services.AddHttpClient<InternetConnectivityService>();

    builder.Services.AddSingleton<MarkdownRenderer>();
    builder.Services.AddHttpClient<ReadMeService>();
    builder.Services.AddTransient<AutomatedJudgeService>();
    builder.Services.AddHttpClient<IOpenRouterCatalogService, OpenRouterCatalogService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    });

    // Clientes nomeados para os juízes automáticos (Gemini e OpenRouter)
    builder.Services.AddHttpClient("Gemini", client => client.Timeout = TimeSpan.FromSeconds(30));
    builder.Services.AddHttpClient("OpenRouter", client => client.Timeout = TimeSpan.FromSeconds(120));
    builder.Services.AddScoped<IBenchmarkRepository, BenchmarkRepository>();
    builder.Services.AddScoped<ISettingsRepository, SettingsRepository>();
    builder.Services.AddScoped<IModelosJuizRepository, ModelosJuizRepository>();
    builder.Services.AddScoped<ILogRepository, LogRepository>();
    builder.Services.AddScoped<IConversationRepository, ConversationRepository>();

    builder.Services.AddHttpClient();

    builder.Services.AddHttpClient<ITranslationService, TranslationService>(client =>
    {
        client.BaseAddress = new Uri(ollamaBaseUrl);
        client.Timeout = TimeSpan.FromMinutes(4);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    });

    // Cliente nomeado para o streaming do chat: timeout alargado porque o Ollama
    // pode demorar muito tempo antes de devolver o primeiro byte (carregamento + KV cache).
    builder.Services.AddHttpClient("Ollama", client =>
    {
        client.BaseAddress = new Uri(ollamaBaseUrl);
        client.Timeout = TimeSpan.FromMinutes(10);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    });

    // Em deployments HTTP-only (ex.: IIS em porta sem certificado), o redirect/HSTS
    // para HTTPS causaria um loop. Controlado via "Security:EnableHttpsRedirection".
    var enableHttpsRedirection = builder.Configuration.GetValue<bool>("Security:EnableHttpsRedirection", true);

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<IDapperContext>();
        DatabaseSchemaInitializer.EnsureSchema(context);
    }

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        if (enableHttpsRedirection)
        {
            app.UseHsts();
        }
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

    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    if (enableHttpsRedirection)
    {
        app.UseHttpsRedirection();
    }
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