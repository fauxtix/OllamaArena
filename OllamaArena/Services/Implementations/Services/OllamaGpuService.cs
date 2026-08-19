using Microsoft.Extensions.Options;
using OllamaArena.Services.Interfaces.Repositories;
using OllamaArena.Services.Interfaces.Services;
using System.Management;
using System.Text;
using System.Text.Json;
using static OllamaArena.Models.DTO.OllamaModels;

namespace OllamaArena.Services.Implementations.Services;

public class OllamaGpuService : IOllamaGpuService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaGpuService> _logger;
    private readonly OllamaOptions _options;
    private string OllamaUrl => $"{_options.BaseUrl}/api/tags";
    private string OllamaPsUrl => $"{_options.BaseUrl}/api/ps";
    private string OllamaChatUrl => $"{_options.BaseUrl}/api/chat";
    private string OllamaShowUrl => $"{_options.BaseUrl}/api/show";
    private readonly IBenchmarkRepository _benchmarks;



    public OllamaGpuService(HttpClient httpClient, ILogger<OllamaGpuService> logger, IOptions<OllamaOptions> options, IBenchmarkRepository benchmarks)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
        _benchmarks = benchmarks;
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

            if (!response.IsSuccessStatusCode) return _options.DefaultContextLength;

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

        return _options.DefaultContextLength;
    }

    /// <summary>
    /// Lê a VRAM livre em bytes. Tenta nvidia-smi primeiro (Windows + Linux, NVIDIA),
    /// depois WMI (apenas Windows) como fallback. Devolve 0 se não for possível detetar.
    /// </summary>
    private async Task<long> GetUsableVramBytesAsync()
    {
        var nvidia = await TryGetVramViaNvidiaSmiAsync();
        if (nvidia.HasValue) return nvidia.Value.FreeBytes;
        return GetAvailableVramInBytes();
    }

    /// <summary>
    /// Consulta o nvidia-smi (embutido nos drivers NVIDIA) para obter a memória total e livre.
    /// Unidades devolvidas pelo nvidia-smi: MiB. Executa de forma assíncrona e espera a
    /// saída do processo antes de ler o stdout (evita o bloqueio/deadlock do ReadToEnd síncrono).
    /// </summary>
    private static async Task<(long TotalBytes, long FreeBytes)?> TryGetVramViaNvidiaSmiAsync()
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

            var outputTask = process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            string output = await outputTask;

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
    public async Task<GpuStatus> CheckGpuCompatibility(long modelSizeInBytes)
    {
        try
        {
            long vramBytes = await GetUsableVramBytesAsync();

            if (vramBytes == 0)
            {
                return new GpuStatus { FitsInGpu = false, AvailableVramGB = 0, EstimatedRequiredMemoryGB = 0 };
            }

            double estimatedRequiredBytes = modelSizeInBytes * _options.ModelLoadOverheadFactor;

            long windowsOverheadBytes = _options.WindowsOverheadMb * 1024 * 1024;
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
        int contextLength = _options.DefaultContextLength; // Valor padrão seguro
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
    /// Verifica via /api/show se o modelo suporta reasoning (capacidade "thinking").
    /// Devolve false por defeito em caso de erro ou de resposta sem a capacidade —
    /// assim nunca envia `think` para modelos que o rejeitam (Ollama devolve 400).
    /// </summary>
    public async Task<bool> ModelSupportsThinkingAsync(string modelName)
    {
        try
        {
            var payload = new { name = modelName };
            var response = await _httpClient.PostAsJsonAsync(OllamaShowUrl, payload);

            if (!response.IsSuccessStatusCode) return false;

            var showData = await response.Content.ReadFromJsonAsync<OllamaShowResponse>();
            return showData?.Capabilities?.Contains("thinking", StringComparer.OrdinalIgnoreCase) == true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao verificar capacidade de thinking de {ModelName}.", modelName);
            return false;
        }
    }

    /// <summary>
    /// Calcula automaticamente o tamanho de contexto efetivo (num_ctx) a partir dos recursos
    /// disponíveis (VRAM detetada) e da arquitetura do modelo. Transparente para o utilizador.
    /// </summary>
    public async Task<int> GetRecommendedContextLengthAsync(long modelSizeInBytes, string modelName)
    {
        int safeDefault = _options.DefaultContextLength;

        try
        {
            long usableVram = await GetUsableVramBytesAsync();

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
            long modelFootprint = (long)(modelSizeInBytes * _options.ModelLoadOverheadFactor);
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

            long windowsOverheadBytes = _options.WindowsOverheadMb * 1024 * 1024;
            long reserveBytes = (long)(usableVram * _options.VramReserveFraction);

            long availableForKv = usableVram - modelFootprint - windowsOverheadBytes - reserveBytes;
            if (availableForKv <= 0)
                return Math.Min(safeDefault, nativeContext);

            long tokensByVram = availableForKv / kvBytesPerToken;
            int maxAllowed = Math.Min(nativeContext, _options.MaxContextCap);
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

    public async Task<string?> GerarResumoDoPromptAsync(string userPrompt, string activeModel)
    {
        try
        {
            if (_httpClient == null || string.IsNullOrWhiteSpace(activeModel)) return null;

            var payload = new
            {
                model = activeModel, // CORREÇÃO: Usa o modelo que já está na VRAM!
                messages = new[]
                {
                new
                {
                    role = "user",
                    content = $"Cria um título/resumo do seguinte prompt em no máximo 5 palavras. Responde APENAS com o resumo, sem aspas ou pontuação.\n\nPrompt: \"{userPrompt}\""
                }
            },
                stream = false,
                options = new { temperature = 0.2 }
            };

            var json = JsonSerializer.Serialize(payload);
            using var request = new HttpRequestMessage(HttpMethod.Post, OllamaChatUrl);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonResponse);

            if (doc.RootElement.TryGetProperty("message", out var msg) &&
                msg.TryGetProperty("content", out var content))
            {
                return content.GetString()?.Trim().Trim('"', '.');
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Falha ao gerar resumo do prompt do utilizador.");
        }

        return null;
    }
}
