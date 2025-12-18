using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NI.Services.AI
{
    public class OllamaClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _model;

        public OllamaClient(string baseUrl = "http://localhost:11434",
                            string model = "llama2")
        {
            _baseUrl = baseUrl;
            _model = model;
            _httpClient = new HttpClient { Timeout = System.TimeSpan.FromSeconds(30) };
        }

        public async Task<string> GenerateAsync(string prompt,
                                                CancellationToken ct = default)
        {
            var request = new
            {
                model = _model,
                prompt = prompt,
                stream = false
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_baseUrl}/api/generate",
                request,
                ct);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaResponse>(cancellationToken: ct);
            return result?.Response ?? string.Empty;
        }

        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }

    internal class OllamaResponse
    {
        public string? Response { get; set; }
        public bool Done { get; set; }
    }
}
