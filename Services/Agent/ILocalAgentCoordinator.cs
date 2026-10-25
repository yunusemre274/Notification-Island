using NI.Agent;

namespace NI.Services.Agent;

// The view model depends on task orchestration, not a concrete LLM or Windows adapter.
public interface ILocalAgentCoordinator : IDisposable
{
    event EventHandler<TaskSnapshot>? Changed;
    event EventHandler<ApprovalRequest>? ApprovalRequired;
    Task<TaskSnapshot> RunAsync(string request, CancellationToken token);
    Task<ToolResult> UndoAsync(CancellationToken token);
    Task<string> ReadDetailsAsync(CancellationToken token);
    void Cancel();
    bool Approve(string hash, bool accepted);
    void RecoverInterrupted();
}
