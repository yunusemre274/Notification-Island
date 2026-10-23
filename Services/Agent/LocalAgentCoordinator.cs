using System.Diagnostics;
using System.IO;
using System.Text.Json;
using NI.Agent;
using NI.Agent.Alarms;
using NI.Agent.Files;
using NI.Agent.Ollama;
using NI.Agent.Tools;
using NI.Models;

namespace NI.Services.Agent;

public sealed class LocalAgentCoordinator(AppSettings settings, AlarmService alarms,
    Func<string, JsonElement, ToolContext, CancellationToken, Task<ToolResult>> systemTools) : IDisposable
{
    private readonly string _directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NotificationIsland", "agent");
    private AgentRuntime? _runtime;
    private long _lastProgress;
    private bool _disposed;
    public TaskSnapshot? Current { get; private set; }
    public bool IsActive => _runtime?.IsActive == true;
    public event EventHandler<TaskSnapshot>? Changed;
    public event EventHandler<ApprovalRequest>? ApprovalRequired;
    private FileOrganizer Files() => new(WindowsUserFolders.Resolve, new OperationJournal(Path.Combine(_directory, "operations")));
    public async Task<TaskSnapshot> RunAsync(string request, CancellationToken token)
    {
        if (_disposed || IsActive) throw new InvalidOperationException("Bir görev zaten çalışıyor veya ajan kapandı.");
        using var model = new OllamaChatClient(settings.OllamaBaseUrl, settings.OllamaModel, settings.Agent);
        using var runtime = new AgentRuntime(model, DesktopTools.Build(Files(), alarms, settings.Agent, systemTools), settings.Agent);
        _runtime = runtime;
        runtime.Changed += OnChanged; runtime.ApprovalRequired += OnApproval;
        try { return await runtime.RunAsync(request, token); }
        finally { runtime.Changed -= OnChanged; runtime.ApprovalRequired -= OnApproval; _runtime = null; }
    }
    public async Task<ToolResult> UndoAsync(CancellationToken token)
    {
        if (IsActive) return ToolResult.Fail("BUSY", "Önce etkin görevi bitirin veya iptal edin.");
        return await Task.Run(() =>
        {
            var files = Files(); files.ReconcileInterrupted(); var latest = files.LatestUndoable();
            return latest == null ? ToolResult.Fail("NO_UNDO", "Geri alınabilir işlem yok.") : files.Undo(latest.Id, token);
        }, token);
    }
    public Task<string> ReadDetailsAsync(CancellationToken token) => Task.Run(() =>
    {
        var files = Files(); var operation = files.LatestUndoable();
        if (operation == null) return "Dosya işlem kaydı yok. Alarm sonuçları alarm listesinde gösterilir.";
        var journal = new OperationJournal(Path.Combine(_directory, "operations"));
        var lines = new List<string> { $"{operation.Scope} • {operation.State} • {operation.Completed} taşındı, {operation.Skipped} atlandı" };
        using var reader = new StreamReader(journal.PlanPath(operation.Id));
        for (var i = 0; i < 20 && reader.ReadLine() is { } text; i++)
        {
            token.ThrowIfCancellationRequested();
            var row = JsonSerializer.Deserialize<MoveRow>(text, AgentJson.Options)!;
            var state = journal.Outcome(operation.Id, row.Index)?.State ?? "Yapılmadı";
            lines.Add($"{(row.Name.Length > 80 ? row.Name[..80] : row.Name)} → {row.Category} • {state}");
        }
        if (operation.Total > 20) lines.Add($"İlk 20 / {operation.Total} adım gösteriliyor.");
        return string.Join(Environment.NewLine, lines);
    }, token);
    public void Cancel() => _runtime?.Cancel();
    public bool Approve(string hash, bool accepted) => _runtime?.Approve(hash, accepted) == true;
    public void RecoverInterrupted()
    {
        var path = Path.Combine(_directory, "last-task.json");
        if (!File.Exists(path)) return;
        var previous = DurableStore.Read<TaskSnapshot>(path, 8192);
        if (previous.EndedAt == null) previous = previous with { State = previous.VerifiedChanges > 0 ? AgentTaskState.PartiallyCompleted : AgentTaskState.Failed,
            Summary = "Önceki görev kesildi. Otomatik yeniden yürütülmedi; işlem günlüğü ve geri alma kullanılabilir.", EndedAt = DateTimeOffset.UtcNow, ErrorCode = "INTERRUPTED" };
        DurableStore.Write(path, previous);
        Current = previous; Changed?.Invoke(this, previous);
    }
    private void OnChanged(object? sender, TaskSnapshot state)
    {
        var ticks = Stopwatch.GetTimestamp();
        if (state.EndedAt == null && state.State == AgentTaskState.Running && Stopwatch.GetElapsedTime(_lastProgress, ticks) < TimeSpan.FromMilliseconds(120)) return;
        _lastProgress = ticks; Current = state;
        DurableStore.Write(Path.Combine(_directory, "last-task.json"), state);
        Changed?.Invoke(this, state);
    }
    private void OnApproval(object? sender, ApprovalRequest request) => ApprovalRequired?.Invoke(this, request);
    public void Dispose() { _disposed = true; Cancel(); }
}
