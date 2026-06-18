using OllamaFluentUIChat.Services.Interfaces.Services;
using System.Management;
using System.Text.Json;
using static OllamaFluentUIChat.Models.DTO.OllamaModels;

namespace OllamaFluentUIChat.Services.Implementations.Services
{
    public class OllamaGpuService : IOllamaGpuService
    {
        private readonly HttpClient _httpClient;
        private const string OllamaUrl = "http://localhost:11434/api/tags";

        public OllamaGpuService()
        {
            _httpClient = new HttpClient();
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
                throw new Exception("Não foi possível ligar ao Ollama. Verifique se o serviço está ativo.", ex);
            }
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
                // Se falhar (ex: permissões ou ambiente não-Windows), retorna 0
            }
            return 0;
        }

        /// <summary>
        /// Pega no tamanho do modelo (SizeInBytes) e faz o cálculo matemático,
        /// retornando o resultado preenchido na classe 'GpuStatus'.
        /// </summary>
        public GpuStatus CheckGpuCompatibility(long modelSizeInBytes)
        {
            long vramBytes = GetAvailableVramInBytes();

            if (vramBytes == 0)
            {
                return new GpuStatus { FitsInGpu = false, AvailableVramGB = 0, EstimatedRequiredMemoryGB = 0 };
            }

            // 1. Margem de segurança para o Contexto (KV Cache)
            double estimatedRequiredBytes = modelSizeInBytes * 1.2;

            // 2. CRÍTICO PARA O TEU PC: Subtrair o lixo que o Windows/Browser já estão a gastar
            // Vamos assumir que o SO consome cerca de 350 MB fixos da tua gráfica
            long windowsOverheadBytes = 350L * 1024 * 1024;
            long realUsableVramBytes = vramBytes - windowsOverheadBytes;

            if (realUsableVramBytes < 0) realUsableVramBytes = 0;

            // 3. Conversão para Gigabytes para a Interface Gráfica
            double usableVramGB = (double)realUsableVramBytes / (1024 * 1024 * 1024);
            double requiredGB = estimatedRequiredBytes / (1024 * 1024 * 1024);

            // 4. A decisão agora é baseada na VRAM útil que resta!
            return new GpuStatus
            {
                FitsInGpu = realUsableVramBytes > estimatedRequiredBytes,
                AvailableVramGB = Math.Round(usableVramGB, 2),
                EstimatedRequiredMemoryGB = Math.Round(requiredGB, 2)
            };
        }
    }
}
