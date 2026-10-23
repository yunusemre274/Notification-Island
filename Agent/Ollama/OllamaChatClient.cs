using System.Net.Http;
using System.IO;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace NI.Agent.Ollama;

public sealed class OllamaChatClient : IAgentModel
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly AgentOptions _options;
    private bool _validated;
    public string ReleaseStatus { get; private set; } = "NotRequested";
    public OllamaChatClient(string endpoint, string model, AgentOptions options, HttpMessageHandler? handler = null)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || !uri.IsLoopback || uri.Scheme is not ("http" or "https")
            || uri.UserInfo.Length > 0 || uri.AbsolutePath != "/" || uri.Query.Length > 0)
            throw new AgentException("LOCAL_ENDPOINT_REQUIRED", "Yalnızca loopback Ollama endpoint’i kullanılabilir.");
        if (string.IsNullOrWhiteSpace(model) || model.Length > 100 || model.Contains("cloud", StringComparison.OrdinalIgnoreCase)
            || model.Any(char.IsWhiteSpace)) throw new AgentException("LOCAL_MODEL_REQUIRED", "Yerel kurulu, araç destekli bir model seçin.");
        _model = model; _options = options;
        _http = new HttpClient(handler ?? new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false,
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(20), MaxConnectionsPerServer = 1 })
        { BaseAddress = uri, Timeout = Timeout.InfiniteTimeSpan };
    }
    public async Task ValidateAsync(CancellationToken token)
    {
        if (_validated) return;
        using var models = await SendAsync(HttpMethod.Get, "api/tags", null, token);
        var local = models.RootElement.GetProperty("models").EnumerateArray().Any(x =>
            x.GetProperty("name").GetString() is { } name && (name == _model || name == _model + ":latest") &&
            (!x.TryGetProperty("size", out var size) || size.GetInt64() > 0));
        if (!local) throw new AgentException("MODEL_NOT_INSTALLED", "Seçili model yerel olarak kurulu değil. Ayarlardan kurulu bir model seçin; otomatik indirme yapılmadı.");
        using var metadata = await SendAsync(HttpMethod.Post, "api/show", new { model = _model, verbose = false }, token);
        var root = metadata.RootElement;
        if (root.TryGetProperty("remote_host", out _) || root.TryGetProperty("remote_model", out _))
            throw new AgentException("CLOUD_MODEL_DENIED", "Uzak modele yönlenen Ollama modeli reddedildi.");
        if (!root.TryGetProperty("capabilities", out var capabilities) || !capabilities.EnumerateArray().Any(x => x.GetString() == "tools"))
            throw new AgentException("TOOLS_UNSUPPORTED", "Seçili model araç çağırma desteği bildirmiyor.");
        _validated = true;
    }
    public async Task<ModelTurn> ChatAsync(IReadOnlyList<ChatMessage> messages, IReadOnlyList<ToolDefinition> tools, CancellationToken token)
    {
        var payload = new
        {
            model = _model, stream = false, keep_alive = "30s",
            options = new { num_ctx = _options.ContextTokens, num_predict = 768, temperature = 0 },
            messages = messages.Select(x => new
            {
                role = x.Role, content = x.Content, tool_name = x.ToolName,
                tool_calls = x.Calls?.Select(call => new { function = new { name = call.Name, arguments = call.Arguments } }).ToArray()
            }).ToArray(),
            tools = tools.Select(x => new { type = "function", function = new { name = x.Name, description = x.Description, parameters = x.InputSchema } }).ToArray()
        };
        using var response = await SendAsync(HttpMethod.Post, "api/chat", payload, token);
        var root = response.RootElement;
        if (!root.TryGetProperty("done", out var done) || done.ValueKind != JsonValueKind.True ||
            (root.TryGetProperty("done_reason", out var reason) && reason.GetString() == "length"))
            throw new AgentException("INCOMPLETE_RESPONSE", "Tamamlanmamış model yanıtı çalıştırılmadı.");
        var message = root.GetProperty("message");
        var calls = new List<ToolCall>();
        if (message.TryGetProperty("tool_calls", out var list))
        {
            if (list.ValueKind != JsonValueKind.Array || list.GetArrayLength() > 8) throw new AgentException("INVALID_TOOL_CALLS", "Geçersiz araç çağrıları.");
            foreach (var call in list.EnumerateArray())
            {
                var function = call.GetProperty("function");
                var name = function.GetProperty("name").GetString();
                var args = function.GetProperty("arguments");
                if (name == null || name.Length > 80 || args.ValueKind != JsonValueKind.Object)
                    throw new AgentException("INVALID_TOOL_CALL", "Eksik veya geçersiz araç çağrısı çalıştırılmadı.");
                calls.Add(new(name, args.Clone()));
            }
        }
        return new(message.TryGetProperty("content", out var content) ? content.GetString() ?? "" : "", calls);
    }
    private async Task<JsonDocument> SendAsync(HttpMethod method, string path, object? data, CancellationToken token)
    {
        using var request = new HttpRequestMessage(method, path);
        if (data != null) request.Content = JsonContent.Create(data, options: AgentJson.Options);
        using var response = await SendRequestAsync(request, token);
        if (!response.IsSuccessStatusCode) throw new AgentException("OLLAMA_HTTP_" + (int)response.StatusCode, "Yerel Ollama isteği başarısız: " + (int)response.StatusCode);
        await using var input = await response.Content.ReadAsStreamAsync(token);
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int read;
        while ((read = await input.ReadAsync(chunk, token)) > 0)
        {
            if (buffer.Length + read > _options.MaxResponseBytes) throw new AgentException("RESPONSE_LIMIT", "Model yanıtı boyut sınırını aştı.");
            await buffer.WriteAsync(chunk.AsMemory(0, read), token);
        }
        try { return JsonDocument.Parse(buffer.GetBuffer().AsMemory(0, (int)buffer.Length), new JsonDocumentOptions { MaxDepth = 20 }); }
        catch (JsonException) { throw new AgentException("MALFORMED_RESPONSE", "Bozuk/kesilmiş model JSON yanıtı çalıştırılmadı."); }
    }
    private async Task<HttpResponseMessage> SendRequestAsync(HttpRequestMessage request, CancellationToken token)
    {
        try { return await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token); }
        catch (HttpRequestException) { throw new AgentException("OLLAMA_UNAVAILABLE", "Yerel Ollama sunucusuna ulaşılamadı. Sunucuyu kendiniz başlatın veya yerel endpoint/model ayarını kontrol edin; bulut fallback kullanılmadı."); }
    }
    public async Task ReleaseAsync(CancellationToken token)
    {
        _validated = false;
        if (_options.ManagedServer && _options.UnloadOwnedModelAfterTask)
        {
            // Only explicit ownership permits unloading. No kill and no global environment mutation.
            using var response = await SendAsync(HttpMethod.Post, "api/generate", new { model = _model, keep_alive = 0, stream = false }, token);
            using var state = await SendAsync(HttpMethod.Get, "api/ps", null, token);
            var remains = state.RootElement.GetProperty("models").EnumerateArray().Any(x => x.GetProperty("name").GetString() is { } name && (name == _model || name == _model + ":latest"));
            ReleaseStatus = remains ? "OwnedModelStillLoaded" : "OwnedModelAbsentFromApiPs";
        }
        else ReleaseStatus = "SharedServer30sRetentionRequested_NotVerified";
        // Shared servers retain a bounded 30s per-request keep_alive; other clients retain ownership.
    }
    public void Dispose() => _http.Dispose();
}
