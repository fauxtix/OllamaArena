namespace OllamaArena.Services.Helpers
{
    public sealed class InternetConnectivityService
    {
        private readonly HttpClient _httpClient;

        public InternetConnectivityService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromSeconds(3);
        }

        public async Task<bool> HasInternetAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Head, "https://www.google.com");
                using var resp = await _httpClient.SendAsync(req, cancellationToken);
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
