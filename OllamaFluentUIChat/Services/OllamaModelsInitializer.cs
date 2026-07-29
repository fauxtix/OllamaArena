using Microsoft.JSInterop;
using OllamaFluentUIChat.Services;
using OllamaFluentUIChat.Services.Interfaces.Services;
using System.Text.Json;

public class OllamaModelsInitializer
{
    private readonly IOllamaGpuService _gpuService;
    private readonly IJSRuntime _js;
    private readonly ILogger<OllamaModelsInitializer> _logger;
    private bool _hasRun = false;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public OllamaModelsInitializer(
        IOllamaGpuService gpuService,
        IJSRuntime js,
        ILogger<OllamaModelsInitializer> logger)
    {
        _gpuService = gpuService;
        _js = js;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        if (_hasRun) return;
        _hasRun = true;

        try
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", "ollamaModels");
            await _js.InvokeVoidAsync("localStorage.removeItem", "ollamaModelsFull");

            var isRunning = await OllamaChecker.IsOllamaRunningAsync();
            if (!isRunning)
            {
                _logger.LogWarning("Ollama não está em execução no arranque.");
                return;
            }

            var response = await _gpuService.GetLocalModelsAsync();
            var fullModels = response.Models ?? [];

            foreach (var model in fullModels)
            {
                var metadata = await _gpuService.GetExtendedModelMetadataAsync(model.Model);
                model.ContextLength = metadata.ContextLength;
                model.TrainingYear = metadata.TrainingYear;
            }

            var names = fullModels.Select(m => m.Model).ToList();

            await _js.InvokeVoidAsync("localStorage.setItem", "ollamaModels",
                JsonSerializer.Serialize(names));

            await _js.InvokeVoidAsync("localStorage.setItem", "ollamaModelsFull",
                JsonSerializer.Serialize(fullModels, JsonOptions));

            _logger.LogInformation("Modelos Ollama carregados com sucesso no arranque.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao inicializar modelos Ollama.");
        }
    }
}