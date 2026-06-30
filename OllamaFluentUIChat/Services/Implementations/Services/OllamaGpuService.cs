using OllamaFluentUIChat.Services.Interfaces.Services;
using System.Management;
using System.Text.Json;
using static OllamaFluentUIChat.Models.DTO.OllamaModels;

namespace OllamaFluentUIChat.Services.Implementations.Services;

public class OllamaGpuService : IOllamaGpuService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaGpuService> _logger;
    private const string OllamaUrl = "http://localhost:11434/api/tags";
    private const string OllamaPsUrl = "http://localhost:11434/api/ps";
    private const string OllamaChatUrl = "http://localhost:11434/api/chat";
    private const string OllamaShowUrl = "http://localhost:11434/api/show";

    public OllamaGpuService(HttpClient httpClient, ILogger<OllamaGpuService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Faz o pedido ao Ollama local e preenche a classe 'OllamaResponse' 
    /// que contém a lista de 'ModelDetails'.
    /// </summary>
    public async Task<OllamaResponse> GetLocalModelsAsync()
    {
        try
        {
            var response = await _httpClient.GetStreamAsync(OllamaUrl);
            var output = await JsonSerializer.DeserializeAsync<OllamaResponse>(response);
            return output ?? new OllamaResponse();
        }
        catch (Exception ex)
        {
            string message = $"Não foi possível ligar ao Ollama. Verifique se o serviço está ativo: {ex.Message}";
            _logger.LogError(message);
            return new();
        }
    }

    /// <summary>
    /// Consulta o endpoint /api/show para obter as propriedades do modelo e extrair o tamanho do contexto (num_ctx).
    /// </summary>
    public async Task<int> GetModelContextLengthAsync(string modelName)
    {
        try
        {
            var payload = new { name = modelName };
            var response = await _httpClient.PostAsJsonAsync(OllamaShowUrl, payload);

            if (!response.IsSuccessStatusCode) return 2048;

            var showData = await response.Content.ReadFromJsonAsync<OllamaShowResponse>();

            if (showData?.ModelInfo != null)
            {
                var contextKey = showData.ModelInfo.Keys
                    .FirstOrDefault(k => k.EndsWith(".context_length", StringComparison.OrdinalIgnoreCase));

                if (contextKey != null && showData.ModelInfo.TryGetValue(contextKey, out var contextValue))
                {
                    if (int.TryParse(contextValue.ToString(), out int parsedLength))
                    {
                        return parsedLength;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Erro ao extrair context_length nativo via model_info: {ex.Message}");
        }

        return 2048;
    }

    /// <summary>
    /// Método auxiliar interno para ler a VRAM do Windows via WMI.
    /// </summary>
    private long GetAvailableVramInBytes()
    {
        try
        {
            using (var searcher = new ManagementObjectSearcher("SELECT AdapterRAM FROM Win32_VideoController"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    var ram = obj["AdapterRAM"];
                    if (ram != null)
                    {
                        return Convert.ToInt64(ram);
                    }
                }
            }
        }
        catch
        {
        }
        return 0;
    }

    /// <summary>
    /// Pega no tamanho do modelo (SizeInBytes) e faz o cálculo matemático,
    /// retornando o resultado preenchido na classe 'GpuStatus'.
    /// </summary>
    public GpuStatus CheckGpuCompatibility(long modelSizeInBytes)
    {
        try
        {
            long vramBytes = GetAvailableVramInBytes();

            if (vramBytes == 0)
            {
                return new GpuStatus { FitsInGpu = false, AvailableVramGB = 0, EstimatedRequiredMemoryGB = 0 };
            }

            double estimatedRequiredBytes = modelSizeInBytes * 1.2;

            long windowsOverheadBytes = 350L * 1024 * 1024;
            long realUsableVramBytes = vramBytes - windowsOverheadBytes;

            if (realUsableVramBytes < 0) realUsableVramBytes = 0;

            double usableVramGB = (double)realUsableVramBytes / (1024 * 1024 * 1024);
            double requiredGB = estimatedRequiredBytes / (1024 * 1024 * 1024);

            return new GpuStatus
            {
                FitsInGpu = realUsableVramBytes > estimatedRequiredBytes,
                AvailableVramGB = Math.Round(usableVramGB, 2),
                EstimatedRequiredMemoryGB = Math.Round(requiredGB, 2)
            };

        }
        catch (Exception ex)
        {
            _logger.LogError($"Erro em CheckGpuCompatibility: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Faz o pedido ao endpoint /api/ps para listar os modelos atualmente carregados na RAM/VRAM.
    /// </summary>
    public async Task<OllamaResponse> GetRunningModelsAsync()
    {
        try
        {
            var response = await _httpClient.GetStreamAsync($"{OllamaPsUrl}");
            var output = await JsonSerializer.DeserializeAsync<OllamaResponse>(response);
            return output ?? new OllamaResponse();
        }
        catch (Exception ex)
        {
            string message = $"Não foi possível obter os modelos ativos no Ollama: {ex.Message}";
            _logger.LogError(message);
            return new();
        }
    }

    /// <summary>
    /// Força o Ollama a libertar e limpar imediatamente os recursos (RAM/VRAM) de um modelo específico.
    /// </summary>
    public async Task<bool> UnloadModelFromMemoryAsync(string modelName)
    {
        try
        {
            var payload = new
            {
                model = modelName,
                messages = new List<object>(),
                keep_alive = 0
            };

            var response = await _httpClient.PostAsJsonAsync(OllamaChatUrl, payload);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            string message = $"Falha ao descarregar o modelo {modelName} da memória: {ex.Message}";
            _logger.LogError(message);
            return false;
        }
    }
}
