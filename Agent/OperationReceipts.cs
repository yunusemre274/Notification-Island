using System.IO;
namespace NI.Agent;

public sealed record OperationReceipt(int Version, string Kind, string State, ToolResult? Result);
public sealed class OperationReceipts(string directory)
{
    private string PathFor(string id) => DurableStore.IdPath(Path.GetFullPath(directory), id) + ".json";
    public ToolResult Execute(string id, string kind, Func<ToolResult> action)
    {
        using (var lease = DurableStore.Lock(directory))
        {
            var path = PathFor(id);
            if (File.Exists(path))
            {
                var receipt = DurableStore.Read<OperationReceipt>(path);
                if (receipt.Version != 1 || receipt.Kind != kind) return ToolResult.Fail("ID_CONFLICT", "İşlem kimliği başka bir eyleme bağlı.", id);
                return receipt.Result is { } result ? result with { VerifiedChanges = 0 }
                    : ToolResult.Fail("RECONCILIATION_REQUIRED", "Önceki işlem sonucu belirsiz. Yeniden yürütülmedi; kayıtların uzlaştırılması gerekli.", id);
            }
            if (Directory.EnumerateFiles(directory, "*.json").Take(1025).Count() >= 1024)
                return ToolResult.Fail("RECEIPT_LIMIT", "İşlem kayıt sınırı dolu; eski günlükler güvenle arşivlenmeli.", id);
            DurableStore.Write(path, new OperationReceipt(1, kind, "Intent", null));
        }
        ToolResult outcome;
        try { outcome = action(); }
        catch (AgentException ex) { outcome = ToolResult.Fail(ex.Code, ex.Message, id); }
        // Unexpected exceptions intentionally leave an uncertain intent, never auto-replayed.
        using (var lease = DurableStore.Lock(directory)) DurableStore.Write(PathFor(id), new OperationReceipt(1, kind, "Finished", outcome));
        return outcome;
    }
    public void Resolve(string id, ToolResult result)
    {
        using var lease = DurableStore.Lock(directory);
        var path = PathFor(id);
        if (!File.Exists(path)) return;
        var receipt = DurableStore.Read<OperationReceipt>(path);
        if (receipt.Result == null || !receipt.Result.Success && receipt.Result.ErrorCode is "SCHEDULER_UNAVAILABLE" or "SCHEDULE_NOT_VERIFIED") DurableStore.Write(path, receipt with { State = "Reconciled", Result = result });
    }
}
