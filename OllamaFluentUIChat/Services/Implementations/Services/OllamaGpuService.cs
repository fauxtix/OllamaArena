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
    /// Lê a VRAM livre em bytes. Tenta nvidia-smi primeiro (Windows + Linux, NVIDIA),
    /// depois WMI (apenas Windows) como fallback. Devolve 0 se não for possível detetar.
    /// </summary>
    private long GetUsableVramBytes()
    {
        var nvidia = TryGetVramViaNvidiaSmi();
        if (nvidia.HasValue) return nvidia.Value.FreeBytes;
        return GetAvailableVramInBytes();
    }

    /// <summary>
    /// Consulta o nvidia-smi (embutido nos drivers NVIDIA) para obter a memória total e livre.
    /// Unidades devolvidas pelo nvidia-smi: MiB.
    /// </summary>
    private static (long TotalBytes, long FreeBytes)? TryGetVramViaNvidiaSmi()
    {
        try
        {
            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "nvidia-smi",
                    Arguments = "--query-gpu=memory.total,memory.free --format=csv,noheader,nounits",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            if (!process.Start()) return null;

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            var line = output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(line)) return null;

            var parts = line.Split(',');
            if (parts.Length < 2) return null;

            if (long.TryParse(parts[0].Trim(), out long totalMiB) &&
                long.TryParse(parts[1].Trim(), out long freeMiB))
            {
                return (totalMiB * 1024 * 1024, freeMiB * 1024 * 1024);
            }
        }
        catch
        {
        }
        return null;
    }

    /// <summary>
    /// Método auxiliar interno para ler a VRAM do Windows via WMI.
    /// </summary>
    private long GetAvailableVramInBytes()
    {
        if (!OperatingSystem.IsWindows()) return 0;

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


    /// <summary>
    /// Procura metadados estendidos (Contexto e Ano de Treino) para um modelo específico via /api/show.
    /// </summary>
    /// <summary>
    /// Extrai o contexto e varre dinamicamente as licenças e ficheiros do modelo 
    /// para isolar o ano real de publicação, sem depender de formatos de nomes.
    /// </summary>
    public async Task<(int ContextLength, string TrainingYear)> GetExtendedModelMetadataAsync(string modelName)
    {
        int contextLength = 2048; // Valor padrão seguro
        string trainingYear = "2024";

        try
        {
            var payload = new { name = modelName };
            var response = await _httpClient.PostAsJsonAsync(OllamaShowUrl, payload);

            if (response.IsSuccessStatusCode)
            {
                var showData = await response.Content.ReadFromJsonAsync<OllamaShowResponse>();

                if (showData != null)
                {
                    // 1. Extração estruturada da janela de contexto (model_info)
                    if (showData.ModelInfo != null)
                    {
                        var contextKey = showData.ModelInfo.Keys
                            .FirstOrDefault(k => k.EndsWith(".context_length", StringComparison.OrdinalIgnoreCase));

                        if (contextKey != null && showData.ModelInfo.TryGetValue(contextKey, out var contextValue) && int.TryParse(contextValue.ToString(), out int parsedLength))
                        {
                            contextLength = parsedLength;
                        }

                        // 2. Primeira tentativa: Capturar qualquer metadado de data explícito no dicionário
                        var dateKey = showData.ModelInfo.Keys
                            .FirstOrDefault(k => k.Contains("date", StringComparison.OrdinalIgnoreCase) ||
                                                 k.Contains("modified", StringComparison.OrdinalIgnoreCase));

                        if (dateKey != null && showData.ModelInfo.TryGetValue(dateKey, out var dateValue))
                        {
                            var matchDate = System.Text.RegularExpressions.Regex.Match(dateValue.ToString() ?? "", @"\b(202[0-9])\b");
                            if (matchDate.Success) return (contextLength, matchDate.Value);
                        }
                    }

                    // 3. Estratégia Universal Absoluta (Varrimento de Licença e Parâmetros)
                    // Agrupa todo o bloco de metadados de texto gerados na criação do GGUF pelo Ollama
                    string blocosDeTexto = string.Join(" ",
                        showData.Modelfile ?? "",
                        showData.Parameters ?? "",
                        showData.Template ?? ""
                    );

                    // Procura por anos na década atual (2020 a 2029) dentro das licenças e manifestos brutos
                    var matches = System.Text.RegularExpressions.Regex.Matches(blocosDeTexto, @"\b(202[0-9])\b");
                    if (matches.Count > 0)
                    {
                        // Obtém o ano mais frequente ou o primeiro ano válido encontrado na estrutura
                        trainingYear = matches[0].Value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Erro ao processar metadados universais para {modelName}: {ex.Message}");
        }

        return (contextLength, trainingYear);
    }

    /// <summary>
    /// Calcula automaticamente o tamanho de contexto efetivo (num_ctx) a partir dos recursos
    /// disponíveis (VRAM detetada) e da arquitetura do modelo. Transparente para o utilizador.
    /// </summary>
    public async Task<int> GetRecommendedContextLengthAsync(long modelSizeInBytes, string modelName)
    {
        const int safeDefault = 2048;

        try
        {
            long usableVram = GetUsableVramBytes();

            int nativeContext = safeDefault;
            int kvBytesPerToken = 1024;

            try
            {
                var payload = new { name = modelName };
                var response = await _httpClient.PostAsJsonAsync(OllamaShowUrl, payload);

                if (response.IsSuccessStatusCode)
                {
                    var showData = await response.Content.ReadFromJsonAsync<OllamaShowResponse>();
                    if (showData?.ModelInfo != null)
                    {
                        var ctxKey = showData.ModelInfo.Keys
                            .FirstOrDefault(k => k.EndsWith(".context_length", StringComparison.OrdinalIgnoreCase));

                        if (ctxKey != null && int.TryParse(showData.ModelInfo[ctxKey].ToString(), out int parsedLength))
                        {
                            nativeContext = parsedLength;
                        }

                        kvBytesPerToken = GetKvBytesPerToken(showData.ModelInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erro ao obter metadados para o contexto efetivo de {modelName}: {ex.Message}");
            }

            // Sem deteção de VRAM ou sem tamanho do modelo → default seguro (2048)
            if (usableVram <= 0 || modelSizeInBytes <= 0)
                return Math.Min(safeDefault, nativeContext);

            // Footprint real do modelo em VRAM: /api/ps se estiver carregado, senão estimativa
            long modelFootprint = (long)(modelSizeInBytes * 1.2);
            try
            {
                var running = await GetRunningModelsAsync();
                var run = running?.Models?.FirstOrDefault(m =>
                    m.Name.Equals(modelName, StringComparison.OrdinalIgnoreCase) ||
                    m.Model.Equals(modelName, StringComparison.OrdinalIgnoreCase));

                if (run != null && run.SizeInVramBytes > 0)
                    modelFootprint = run.SizeInVramBytes;
            }
            catch { }

            const long windowsOverheadBytes = 350L * 1024 * 1024;
            long reserveBytes = (long)(usableVram * 0.10);

            long availableForKv = usableVram - modelFootprint - windowsOverheadBytes - reserveBytes;
            if (availableForKv <= 0)
                return Math.Min(safeDefault, nativeContext);

            long tokensByVram = availableForKv / kvBytesPerToken;
            int maxAllowed = Math.Min(nativeContext, 32768);
            int recommended = (int)Math.Clamp(tokensByVram, 1024L, (long)maxAllowed);

            _logger.LogInformation(
                "Contexto efetivo calculado para {ModelName}: {Context} tokens (VRAM livre: {Vram} MB, footprint: {Footprint} MB, KV: {Kv} B/token)",
                modelName, recommended, usableVram / (1024 * 1024), modelFootprint / (1024 * 1024), kvBytesPerToken);

            return recommended;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Erro a calcular o contexto efetivo para {modelName}: {ex.Message}");
            return safeDefault;
        }
    }

    /// <summary>
    /// Calcula os bytes de KV cache por token a partir da arquitetura do modelo (cross-platform).
    /// Fórmula: 4 × layers × kv_heads × head_dim (2 para K+V × 2 bytes fp16).
    /// </summary>
    private static int GetKvBytesPerToken(Dictionary<string, object> modelInfo, int fallback = 1024)
    {
        try
        {
            string archPrefix = "llama";
            if (modelInfo.TryGetValue("general.architecture", out var archObj))
            {
                var arch = archObj?.ToString();
                if (!string.IsNullOrWhiteSpace(arch))
                    archPrefix = arch;
            }

            double GetValue(string key)
            {
                var fullKey = modelInfo.Keys
                    .FirstOrDefault(k => k.Equals($"{archPrefix}.{key}", StringComparison.OrdinalIgnoreCase));

                if (fullKey == null) return 0;
                return double.TryParse(modelInfo[fullKey].ToString(), out var v) ? v : 0;
            }

            double layers = GetValue("block_count");
            if (layers <= 0) layers = GetValue("attention.layer_count");
            double kvHeads = GetValue("attention.head_count_kv");
            double headCount = GetValue("attention.head_count");
            double embedding = GetValue("embedding_length");

            double headDim = GetValue("attention.key_length");
            if (headDim <= 0) headDim = GetValue("attention.value_length");
            if (headDim <= 0) headDim = GetValue("attention.head_dim");
            if (headDim <= 0 && headCount > 0 && embedding > 0)
                headDim = embedding / headCount;

            if (kvHeads <= 0) kvHeads = headCount;

            if (layers > 0 && kvHeads > 0 && headDim > 0)
            {
                double bytesPerToken = 4 * layers * kvHeads * headDim;
                return (int)Math.Ceiling(bytesPerToken);
            }
        }
        catch { }

        return fallback;
    }

}
