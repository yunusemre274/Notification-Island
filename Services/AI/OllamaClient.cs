using NI.Services.AI;
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
    /// PHASE 4: Ollama HTTP client with proper error handling and auto-start
    /// </summary>
    public class OllamaClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _model;
        private static Process? _ollamaProcess;
        private static readonly object _processLock = new();
        private bool _disposed = false;

        public OllamaClient(string baseUrl = "http://localhost:11434",
                            string model = "qwen2.5-coder:7b")
        {
            _baseUrl = baseUrl;
            _model = model;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

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
/// AGENT MODE: Uses AgentPrompt system rules and returns RAW JSON
/// </summary>
public async Task<string> GenerateAgentAsync(string userInput,
                                             CancellationToken ct = default)
{
    if (string.IsNullOrWhiteSpace(userInput))
        throw new ArgumentException("User input cannot be empty", nameof(userInput));

    var fullPrompt =
        AgentPrompt.SystemPrompt +
        "\nUser: " + userInput;

    return await GenerateAsync(fullPrompt, ct);
}

        /// <summary>
        /// CHAT MODE: Uses llama3.1 model for conversational responses
        /// </summary>
        public async Task<string> GenerateChatAsync(string userInput,
                                                     CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userInput))
                throw new ArgumentException("User input cannot be empty", nameof(userInput));

            try
            {
                Debug.WriteLine($"[CHAT] Chat request: {userInput}");

                var request = new
                {
                    model = "llama3.1",
                    prompt = userInput,
                    stream = false
                };

                var response = await _httpClient.PostAsJsonAsync(
                    $"{_baseUrl}/api/generate",
                    request,
                    ct);

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[CHAT] Ollama HTTP error: {response.StatusCode}");
                    throw new OllamaException(response.StatusCode, "Ollama API request failed");
                }

                var result = await response.Content.ReadFromJsonAsync<OllamaResponse>(cancellationToken: ct);
                var answer = result?.Response ?? string.Empty;

                Debug.WriteLine($"[CHAT] Chat response length: {answer.Length} chars");
                return answer;
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"[CHAT] Ollama connection error: {ex.Message}");
                throw new OllamaException(HttpStatusCode.ServiceUnavailable, "Cannot connect to Ollama", ex);
            }
            catch (TaskCanceledException ex)
            {
                Debug.WriteLine($"[CHAT] Ollama timeout: {ex.Message}");
                throw new OllamaException(HttpStatusCode.RequestTimeout, "Ollama request timed out", ex);
            }
        }


        /// <summary>
        /// CRITICAL: Check if Ollama is installed and running, auto-start if needed
        /// Verifies both installation (command exists) and API availability
        /// </summary>
        public async Task<(bool Installed, bool Running, string Message)> VerifyOllamaAsync()
        {
            try
            {
                Debug.WriteLine("[CRITICAL] Verifying Ollama installation and status...");

                // 1. Check if ollama command exists (installation check)
                var processInfo = new ProcessStartInfo
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
                    using var process = Process.Start(processInfo);
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
                bool running = await IsApiRunningAsync();

                if (running)
                {
                    Debug.WriteLine("[CRITICAL] Ollama installed and running ✓");
                    return (true, true, "Ollama ready");
                }

                // 3. Auto-start Ollama if installed but not running
                Debug.WriteLine("[CRITICAL] Ollama installed but not running - attempting auto-start...");
                bool started = await StartOllamaAsync();

                if (started)
                {
                    Debug.WriteLine("[CRITICAL] Ollama auto-started successfully ✓");
                    return (true, true, "Ollama started successfully");
                }
                else
                {
                    Debug.WriteLine("[CRITICAL] Failed to auto-start Ollama");
                    return (true, false, "Failed to start Ollama. Please run 'ollama serve' manually.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CRITICAL] Ollama verification failed: {ex.Message}");
                return (false, false, $"Verification error: {ex.Message}");
            }
        }

        /// <summary>
        /// AUTO-START: Start Ollama serve process in background
        /// </summary>
        private async Task<bool> StartOllamaAsync()
        {
            lock (_processLock)
            {
                // Don't start if already running
                if (_ollamaProcess != null && !_ollamaProcess.HasExited)
                {
                    Debug.WriteLine("[AUTO-START] Ollama process already running");
                    return true;
                }

                try
                {
                    Debug.WriteLine("[AUTO-START] Starting Ollama serve...");

                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "ollama",
                        Arguments = "serve",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    _ollamaProcess = Process.Start(startInfo);

                    if (_ollamaProcess == null)
                    {
                        Debug.WriteLine("[AUTO-START] Failed to start Ollama process");
                        return false;
                    }

                    Debug.WriteLine($"[AUTO-START] Ollama process started (PID: {_ollamaProcess.Id})");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AUTO-START] Error starting Ollama: {ex.Message}");
                    return false;
                }
            }

            // Wait for API to become available (max 10 seconds)
            Debug.WriteLine("[AUTO-START] Waiting for Ollama API to become available...");
            for (int i = 0; i < 20; i++)
            {
                await Task.Delay(500);
                if (await IsApiRunningAsync())
                {
                    Debug.WriteLine($"[AUTO-START] Ollama API available after {(i + 1) * 500}ms");
                    return true;
                }
            }

            Debug.WriteLine("[AUTO-START] Timeout waiting for Ollama API");
            return false;
        }

        /// <summary>
        /// Quick check if Ollama API is responding
        /// </summary>
        private async Task<bool> IsApiRunningAsync()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags", cts.Token);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Simple API availability check (faster, for repeat calls)
        /// </summary>
        public async Task<bool> IsAvailableAsync()
        {
            Debug.WriteLine("[CRITICAL] Quick Ollama API check...");
            bool available = await IsApiRunningAsync();
            Debug.WriteLine($"[CRITICAL] Ollama API available: {available}");
            return available;
        }

        /// <summary>
        /// Cleanup: Stop Ollama process if we started it
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _httpClient?.Dispose();

            // Note: We don't kill the Ollama process on disposal
            // It should keep running for other applications to use
            // Only clean up if explicitly needed

            _disposed = true;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Manually stop the Ollama process (if started by this client)
        /// Call this only when shutting down the application
        /// </summary>
        public static void StopOllamaProcess()
        {
            lock (_processLock)
            {
                if (_ollamaProcess != null && !_ollamaProcess.HasExited)
                {
                    try
                    {
                        Debug.WriteLine("[CLEANUP] Stopping Ollama process...");
                        _ollamaProcess.Kill(true);
                        _ollamaProcess.Dispose();
                        _ollamaProcess = null;
                        Debug.WriteLine("[CLEANUP] Ollama process stopped");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CLEANUP] Error stopping Ollama: {ex.Message}");
                    }
                }
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
