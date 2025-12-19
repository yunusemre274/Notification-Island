using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NI.Services.AI
{
    /// <summary>
    /// PHASE 4: Ollama HTTP client with proper error handling
    /// </summary>
    public class OllamaClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _model;

        public OllamaClient(string baseUrl = "http://localhost:11434",
                            string model = "llama3.2")
        {
            _baseUrl = baseUrl;
            _model = model;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

            Debug.WriteLine($"[PHASE4] OllamaClient initialized: {_baseUrl}, model: {_model}");
        }

        /// <summary>
        /// PHASE 4: Generate response from Ollama with proper error handling
        /// </summary>
        public async Task<string> GenerateAsync(string prompt,
                                                CancellationToken ct = default)
        {
            try
            {
                Debug.WriteLine($"[PHASE4] Ollama request: {prompt}");

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

                // PHASE 4: Specific error handling for different HTTP status codes
                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[PHASE4] Ollama HTTP error: {response.StatusCode}");
                    throw new OllamaException(response.StatusCode, "Ollama API request failed");
                }

                var result = await response.Content.ReadFromJsonAsync<OllamaResponse>(cancellationToken: ct);
                var answer = result?.Response ?? string.Empty;

                Debug.WriteLine($"[PHASE4] Ollama response length: {answer.Length} chars");
                return answer;
            }
            catch (HttpRequestException ex)
            {
                // PHASE 4: Connection refused or network error
                Debug.WriteLine($"[PHASE4] Ollama connection error: {ex.Message}");
                throw new OllamaException(HttpStatusCode.ServiceUnavailable, "Cannot connect to Ollama", ex);
            }
            catch (TaskCanceledException ex)
            {
                // PHASE 4: Timeout
                Debug.WriteLine($"[PHASE4] Ollama timeout: {ex.Message}");
                throw new OllamaException(HttpStatusCode.RequestTimeout, "Ollama request timed out", ex);
            }
        }

        /// <summary>
        /// CRITICAL: Check if Ollama is installed and running
        /// Verifies both installation (command exists) and API availability
        /// </summary>
        public async Task<(bool Installed, bool Running, string Message)> VerifyOllamaAsync()
        {
            try
            {
                Debug.WriteLine("[CRITICAL] Verifying Ollama installation and status...");

                // 1. Check if ollama command exists (installation check)
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ollama",
                    Arguments = "list",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                bool installed = false;
                try
                {
                    using var process = System.Diagnostics.Process.Start(processInfo);
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                        installed = process.ExitCode == 0;
                    }
                }
                catch
                {
                    installed = false;
                }

                if (!installed)
                {
                    Debug.WriteLine("[CRITICAL] Ollama not installed");
                    return (false, false, "Ollama not installed. Install from ollama.com");
                }

                // 2. Check if Ollama API is running
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags");
                bool running = response.IsSuccessStatusCode;

                if (running)
                {
                    Debug.WriteLine("[CRITICAL] Ollama installed and running ✓");
                    return (true, true, "Ollama ready");
                }
                else
                {
                    Debug.WriteLine("[CRITICAL] Ollama installed but not running");
                    return (true, false, "Ollama installed but not running. Run 'ollama serve'");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CRITICAL] Ollama verification failed: {ex.Message}");
                return (false, false, $"Verification error: {ex.Message}");
            }
        }

        /// <summary>
        /// Simple API availability check (faster, for repeat calls)
        /// </summary>
        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                Debug.WriteLine("[CRITICAL] Quick Ollama API check...");
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags");
                bool available = response.IsSuccessStatusCode;
                Debug.WriteLine($"[CRITICAL] Ollama API available: {available}");
                return available;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CRITICAL] Ollama API not available: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// PHASE 4: Custom exception for Ollama errors with HTTP status code
    /// </summary>
    public class OllamaException : Exception
    {
        public HttpStatusCode StatusCode { get; }

        public OllamaException(HttpStatusCode statusCode, string message, Exception? innerException = null)
            : base(message, innerException)
        {
            StatusCode = statusCode;
        }
    }

    internal class OllamaResponse
    {
        public string? Response { get; set; }
        public bool Done { get; set; }
    }
}
