namespace OllamaFluentUIChat.Services
{
    public static class OllamaChecker
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5) // Timeout razoável
        };

    /// <summary>
    /// Checks if the Ollama API is reachable by attempting a request to the /api/version endpoint.
    /// </summary>
    /// <param name="baseUrl">
    /// Ollama base url
    /// </param>
    /// <returns>True if the server responds with a success status code; otherwise, false.</returns>
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
