namespace OllamaFluentUIChat.Services
{
    public static class OllamaChecker
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5) // Timeout razoável
        };

        /// <summary>
        /// Verifica se o serviço do Ollama está rodando.
        /// </summary>
        /// <param name="baseUrl">URL base do Ollama (padrão: http://localhost:11434)</param>
        /// <returns>True se o serviço estiver respondendo, False caso contrário.</returns>
        public static async Task<bool> IsOllamaRunningAsync(string baseUrl = "http://localhost:11434")
        {
            try
            {
                string url = $"{baseUrl.TrimEnd('/')}/api/version";
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    string content = await response.Content.ReadAsStringAsync();
                    return content.Contains("version");
                }

                return false;
            }
            catch (Exception)
            {
                // Qualquer erro (timeout, conexão recusada, etc.) significa que não está rodando
                return false;
            }
        }
    }
}
