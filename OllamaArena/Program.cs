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
    // Handlers globais do Dapper para converter valores INTEGER do SQLite
    // (ex.: Int64 4) em double/float sem InvalidCastException.
    SqliteTypeHandlers.Register();

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

    // Pesos das 12 métricas dos juízes (ScoreCalculator/ranking), configuráveis
    // em appsettings.json — permite rebalancear sem alterar código.
    builder.Services.Configure<JudgeScoreWeights>(builder.Configuration.GetSection(JudgeScoreWeights.SectionName));

    var ollamaBaseUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";

    builder.Services.AddTransient<IDapperContext, DapperContext>();
    builder.Services.AddTransient<IOllamaGpuService, OllamaGpuService>();

    builder.Services.AddTransient<IPromptTemplateProvider, PromptTemplateProvider>();
    builder.Services.AddTransient<PromptFilesService>();
    builder.Services.AddTransient<ChatComposerService>();
    builder.Services.AddTransient<EvaluatePromptTemplate>();

    // Chat: streaming e persistência de turnos isolados da página.
    builder.Services.AddTransient<IChatStreamClient, OllamaChatStreamClient>();
    builder.Services.AddTransient<IChatTurnWriter, ChatTurnWriter>();

    // Planilha de benchmarks: gera o .xlsx e serve-o por HTTP, para não passar
    // os bytes em base64 pelo circuito SignalR.
    builder.Services.AddScoped<IBenchmarkExcelExporter, BenchmarkExcelExporter>();

    // A análise local é 100% CPU/in memória (sem HTTP): registada como serviço
    // normal, não como cliente tipado — o AddHttpClient anterior injetava um
    // HttpClient que o serviço nunca usava.
    builder.Services.AddScoped<IAnalysisService, LocalAnalysisService>();
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
    builder.Services.AddHttpClient("Gemini", client => client.Timeout = TimeSpan.FromSeconds(120));
    builder.Services.AddHttpClient("OpenRouter", client => client.Timeout = TimeSpan.FromSeconds(120));
    // D2: o repositório de benchmarks foi partido em três responsabilidades (comandos,
// read models e agregações). As três implementações partilham o mesmo IDapperContext
// e o mesmo lifetime, por isso a ligação continua a ser uma só.
    builder.Services.AddScoped<IBenchmarkCommandRepository, BenchmarkCommandRepository>();
    builder.Services.AddScoped<IBenchmarkQueryRepository, BenchmarkQueryRepository>();
    builder.Services.AddScoped<IBenchmarkStatsRepository, BenchmarkStatsRepository>();
    builder.Services.AddScoped<ISettingsRepository, SettingsRepository>();
    builder.Services.AddScoped<IModelosJuizRepository, ModelosJuizRepository>();
    builder.Services.AddScoped<ILogRepository, LogRepository>();
    builder.Services.AddScoped<IConversationRepository, ConversationRepository>();

    // Credenciais/modelos dos provedores de IA (BD → configuração → default),
    // partilhados pelos juízes automáticos e pela cadeia de tradução.
    builder.Services.AddScoped<ApiCredentialsResolver>();

    builder.Services.AddHttpClient();

    // Cliente tipado da tradução: apenas para o último recurso (Ollama local).
    // OpenRouter e Gemini usam os clientes nomeados abaixo.
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

    // ---------- DESCARGA DA PLANILHA DE BENCHMARKS ----------
    // O .xlsx é gerado pelo componente, gravado numa pasta temporária e servido
    // daqui. Passar os bytes em base64 pelo circuito SignalR contava com o limite
    // de 32 MB e rebentava em exportações grandes; por HTTP não há esse teto.
    const string ExcelMimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    app.MapGet("/api/export/benchmarks/{identificador}", IResult (string identificador, HttpContext http) =>
    {
        // O caminho é reconstruído a partir de um Guid validado, nunca a partir do
        // texto recebido: "../" ou barras não produzem um caminho utilizável.
        var caminho = BenchmarkExcelExporter.ResolverCaminho(identificador);

        if (caminho is null || !File.Exists(caminho))
            return Results.NotFound();

        // A exportação é de uso único: o ficheiro apaga-se assim que a resposta
        // terminar de ser enviada. (.NET 10 removeu deleteAfterSend de Results.File,
        // por isso a limpeza é feita em OnCompleted.) A varredura por idade do
        // exportador continua a ser a rede de segurança para ficheiros órfãos.
        http.Response.OnCompleted(() =>
        {
            try
            {
                File.Delete(caminho);
            }
            catch (IOException)
            {
                // Ficheiro preso por outro processo: a limpeza por idade apanha-o.
            }
            catch (UnauthorizedAccessException)
            {
            }

            return Task.CompletedTask;
        });

        return Results.File(caminho, ExcelMimeType);
    });

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