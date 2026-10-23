using System.IO;
namespace NI.Agent.Files;

public sealed record MoveRow(int Index, string Name, string Category, FileStamp Stamp);
public sealed record MoveOutcome(string State, string Message, FileStamp? DestinationStamp = null);
public sealed record FileOperation(int Version, string Id, string Scope, string Root, string Policy,
    DateTimeOffset CreatedAt, string State, int Total, int Completed = 0, int Skipped = 0, int NotRun = 0, int Excluded = 0);

public sealed class OperationJournal(string directory)
{
    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    public void EnforceQuota()
    {
        Directory.CreateDirectory(DirectoryPath);
        PathGuard.RejectLinks(DirectoryPath);
        foreach (var folder in Directory.EnumerateDirectories(DirectoryPath))
        {
            var id = Path.GetFileName(folder);
            if (!Guid.TryParseExact(id, "N", out _) || !File.Exists(HeaderPath(id))) continue;
            PathGuard.RejectLinks(folder);
            var record = Read(id);
            if (DateTimeOffset.UtcNow - record.CreatedAt > TimeSpan.FromDays(30) && record.State is "Completed" or "Undone" or "Cancelled")
                Directory.Delete(folder, recursive: true); // Only our completed business journal, never user files.
        }
        if (Directory.EnumerateDirectories(DirectoryPath).Take(128).Count() >= 128)
            throw new AgentException("JOURNAL_LIMIT", "128 işlem sınırı dolu; günlükler güvenle arşivlenmeli.");
        long total = 0;
        foreach (var path in Directory.EnumerateFiles(DirectoryPath, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }))
        {
            total += new FileInfo(path).Length;
            if (total > 64_000_000) throw new AgentException("JOURNAL_LIMIT", "İşlem günlüğü 64 MB sınırını aştı; yeni plan hazırlanmadı.");
        }
    }
    public string Folder(string id) => DurableStore.IdPath(DirectoryPath, id);
    public string HeaderPath(string id) => Path.Combine(Folder(id), "operation.json");
    public FileOperation Read(string id)
    {
        var value = DurableStore.Read<FileOperation>(HeaderPath(id));
        if (value.Version != 1 || value.Id != id) throw new InvalidDataException("Bilinmeyen işlem günlüğü sürümü/kimliği.");
        return value;
    }
    public void Save(FileOperation operation) => DurableStore.Write(HeaderPath(operation.Id), operation);
    public string PlanPath(string id) => Path.Combine(Folder(id), "plan.jsonl");
    public void WriteOutcome(string id, int index, MoveOutcome value) => DurableStore.Write(OutcomePath(id, index), value);
    public MoveOutcome? Outcome(string id, int index) => File.Exists(OutcomePath(id, index)) ? DurableStore.Read<MoveOutcome>(OutcomePath(id, index)) : null;
    private string OutcomePath(string id, int index) => Path.Combine(Folder(id), $"step-{index:D6}.json");
    public FileOperation? Latest()
    {
        if (!Directory.Exists(DirectoryPath)) return null;
        // Only IDs/timestamps retained; never retain document contents in memory.
        return Directory.EnumerateDirectories(DirectoryPath).Select(Path.GetFileName)
            .Where(x => Guid.TryParseExact(x, "N", out _)).Select(x => Read(x!))
            .Where(x => x.State is "Completed" or "PartiallyCompleted" or "UndoPartial").MaxBy(x => x.CreatedAt);
    }
}
