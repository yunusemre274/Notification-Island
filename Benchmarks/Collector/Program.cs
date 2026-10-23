using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;

if (!OperatingSystem.IsWindows()) { Console.Error.WriteLine("Windows performans ölçümü gerekli; bu ortamda doğrulanmadı."); return 2; }
var flags = args.Chunk(2).ToDictionary(x => x[0], x => x.Length == 2 ? x[1] : "");
if (!flags.TryGetValue("--exe", out var executable) || !Path.IsPathFullyQualified(executable))
{ Console.Error.WriteLine("--exe ABSOLUTE_EXE --scenario compact-idle --seconds 600 --sample-ms 100 --out PATH [--task-state PATH]"); return 2; }
var seconds = flags.TryGetValue("--seconds", out var duration) ? int.Parse(duration) : 600;
var period = flags.TryGetValue("--sample-ms", out var interval) ? Math.Clamp(int.Parse(interval), 50, 1000) : 100;
var scenario = flags.GetValueOrDefault("--scenario", "compact-idle");
var output = Path.GetFullPath(flags.GetValueOrDefault("--out", "artifacts/benchmark-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
Directory.CreateDirectory(output);
var started = DateTimeOffset.UtcNow;
var startupStamp = Stopwatch.GetTimestamp();
using var app = Process.Start(new ProcessStartInfo(executable) { UseShellExecute = false }) ?? throw new Exception("Target başlatılamadı.");
var known = new HashSet<int> { app.Id };
var knownOllama = new HashSet<int>();
var samples = new List<Sample>(); var processRows = Path.Combine(output, "processes.jsonl");
using var raw = new StreamWriter(processRows);
using var counters = new Counters();
var previousCpu = new Dictionary<int, double>();
var previousSample = startupStamp;
using var requestClient = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
var nextOllamaRead = DateTimeOffset.MinValue;
using var modelLog = new StreamWriter(Path.Combine(output, "ollama-models.jsonl"));
while ((DateTimeOffset.UtcNow - started).TotalSeconds < seconds)
{
    var sampleStart = Stopwatch.GetTimestamp();
    var now = DateTimeOffset.UtcNow;
    var elapsed = Math.Max(.001, Stopwatch.GetElapsedTime(previousSample, sampleStart).TotalSeconds); previousSample = sampleStart;
    var parents = Native.Parents();
    known.RemoveWhere(pid => !parents.ContainsKey(pid));
    knownOllama.RemoveWhere(pid => !parents.ContainsKey(pid));
    var all = new List<ProcessRow>(); var coverage = parents.Count > 0;
    foreach (var process in Process.GetProcesses())
    {
        using (process)
        {
            try
            {
                var parent = parents.GetValueOrDefault(process.Id);
                var name = process.ProcessName;
                var sameBinary = name.Equals(Path.GetFileNameWithoutExtension(executable), StringComparison.OrdinalIgnoreCase)
                    && process.MainModule?.FileName?.Equals(executable, StringComparison.OrdinalIgnoreCase) == true;
                if (sameBinary || known.Contains(parent)) known.Add(process.Id);
                if (name.Contains("ollama", StringComparison.OrdinalIgnoreCase) || knownOllama.Contains(parent)) knownOllama.Add(process.Id);
                var family = known.Contains(process.Id) ? "application" : knownOllama.Contains(process.Id) ? "ollama" : name == "dwm" ? "dwm-system" : null;
                if (family == null) continue;
                var cpu = process.TotalProcessorTime.TotalSeconds;
                var delta = previousCpu.TryGetValue(process.Id, out var previous) ? Math.Max(0, cpu - previous) : process.StartTime.ToUniversalTime() >= started.UtcDateTime ? cpu : 0;
                previousCpu[process.Id] = cpu;
                var privateWs = Native.PrivateWorkingSet(process.Handle);
                all.Add(new(process.Id, parent, name, family, process.PrivateMemorySize64, privateWs, process.WorkingSet64,
                    delta, process.HandleCount, process.Threads.Count, process.StartTime.ToUniversalTime()));
            }
            catch { if (known.Contains(process.Id)) coverage = false; }
        }
    }
    var application = all.Where(x => x.Family == "application").ToArray();
    var ollama = all.Where(x => x.Family == "ollama").ToArray();
    var phase = "normal"; string? taskId = null; DateTimeOffset? taskEnded = null;
    if (flags.TryGetValue("--task-state", out var statePath) && File.Exists(statePath))
    {
        try
        {
            using var state = JsonDocument.Parse(File.ReadAllText(statePath));
            var root = state.RootElement; taskId = root.GetProperty("id").GetString();
            if (root.TryGetProperty("endedAt", out var ended) && ended.ValueKind != JsonValueKind.Null) taskEnded = ended.GetDateTimeOffset();
            if (Guid.TryParse(taskId, out _) && taskEnded == null && root.GetProperty("state").GetString() is "Planning" or "Running") phase = "active-ai";
            else if (taskEnded.HasValue && now >= taskEnded && now - taskEnded < TimeSpan.FromSeconds(30)) phase = "recovery";
        }
        catch { coverage = false; }
    }
    var gpu = counters.Read(application.Select(x => x.Pid).ToHashSet(), ollama.Select(x => x.Pid).ToHashSet(), all.Where(x => x.Family == "dwm-system").Select(x => x.Pid).ToHashSet());
    var row = new Sample(now, scenario, phase, taskId, taskEnded, application.Sum(x => x.PrivateBytes), SumNullable(application.Select(x => x.PrivateWorkingSet)), application.Sum(x => x.WorkingSet),
        application.Sum(x => x.CpuSeconds), application.Sum(x => x.CpuSeconds) / elapsed / Environment.ProcessorCount * 100,
        application.Sum(x => x.Handles), application.Sum(x => x.Threads), ollama.Sum(x => x.PrivateBytes), SumNullable(ollama.Select(x => x.PrivateWorkingSet)), ollama.Sum(x => x.WorkingSet),
        gpu.AppDedicated, gpu.AppShared, gpu.OllamaDedicated, gpu.OllamaShared, gpu.AppEngine, coverage && application.Length > 0, elapsed);
    samples.Add(row);
    await raw.WriteLineAsync(JsonSerializer.Serialize(new { sample = row, processes = all, systemGpuContext = gpu.Dwm }));
    await raw.FlushAsync();
    if (now >= nextOllamaRead)
    {
        nextOllamaRead = now.AddSeconds(1);
        try { var state = await requestClient.GetStringAsync("http://127.0.0.1:11434/api/ps"); using var models = JsonDocument.Parse(state); await modelLog.WriteLineAsync(JsonSerializer.Serialize(new { timestamp = now, modelState = models.RootElement })); }
        catch { await modelLog.WriteLineAsync(JsonSerializer.Serialize(new { timestamp = now, state = "unavailable" })); }
    }
    var remaining = period - Stopwatch.GetElapsedTime(sampleStart).TotalMilliseconds;
    if (remaining > 0) await Task.Delay((int)remaining);
}
var normal = samples.Where(x => x.Phase == "normal").ToArray();
var invalid = samples.Any(x => !x.CoverageComplete);
var report = new
{
    schemaVersion = 1, started, scenario, requestedSeconds = seconds, requestedSampleMs = period,
    observedMaxIntervalMs = samples.Zip(samples.Skip(1), (a, b) => (b.Timestamp - a.Timestamp).TotalMilliseconds).DefaultIfEmpty().Max(),
    windows = Environment.OSVersion.VersionString, architecture = RuntimeInformation.OSArchitecture.ToString(), logicalProcessors = Environment.ProcessorCount,
    build = flags.GetValueOrDefault("--build", "UNSPECIFIED - record commit/dirty tree separately"),
    hardware = "Record CPU/RAM/GPU, resolution, DPI, rendering mode, active features and model in environment.json",
    memoryBudgetBytes = 20_000_000,
    normalMemoryResult = normal.Length == 0 || invalid ? "UNVERIFIED" : normal.Any(x => x.PrivateBytes > 20_000_000) ? "FAIL" : "PASS_OBSERVED_SAMPLES_ONLY",
    cpuSecondsObserved = normal.Sum(x => x.CpuSeconds),
    normalObservedSeconds = normal.Sum(x => x.IntervalSeconds),
    cpuTimeWeightedPercent = normal.Length == 0 ? (double?)null : normal.Sum(x => x.CpuSeconds) / normal.Sum(x => x.IntervalSeconds) / Environment.ProcessorCount * 100,
    privateBytes = Stats(normal.Select(x => (double)x.PrivateBytes)),
    privateWorkingSetBytes = Stats(normal.Where(x => x.PrivateWorkingSet.HasValue).Select(x => (double)x.PrivateWorkingSet!.Value)),
    totalWorkingSetBytes = Stats(normal.Select(x => (double)x.WorkingSet)),
    handles = Stats(normal.Select(x => (double)x.Handles)), threads = Stats(normal.Select(x => (double)x.Threads)),
    gpuDedicatedBytes = Stats(normal.Where(x => x.GpuDedicated.HasValue).Select(x => (double)x.GpuDedicated!.Value)),
    gpuSharedBytes = Stats(normal.Where(x => x.GpuShared.HasValue).Select(x => (double)x.GpuShared!.Value)),
    gpuBusiestEnginePercent = Stats(normal.Where(x => x.GpuPercent.HasValue).Select(x => x.GpuPercent!.Value)),
    cpuNormalizedPercent = Stats(normal.Select(x => x.CpuNormalizedPercent)),
    idleCpuResult = scenario != "compact-idle" || seconds < 600 || invalid || normal.Length == 0 || normal.Sum(x => x.IntervalSeconds) < 600 ? "UNVERIFIED" : normal.Sum(x => x.CpuSeconds) / normal.Sum(x => x.IntervalSeconds) / Environment.ProcessorCount * 100 < .2 ? "PASS_OBSERVED" : "FAIL",
    ollamaPrivateBytes = Stats(samples.Select(x => (double)x.OllamaPrivateBytes)),
    combinedPrivateBytes = Stats(samples.Select(x => (double)(x.PrivateBytes + x.OllamaPrivateBytes))),
    recovery = samples.Where(x => x.TaskEndedAt.HasValue && x.TaskEndedAt >= started).GroupBy(x => x.TaskId).Select(group => new
    {
        taskId = group.Key, endedAt = group.First().TaskEndedAt,
        secondsToObservedBudget = group.Where(x => x.PrivateBytes <= 20_000_000 && x.CoverageComplete).Select(x => (double?)(x.Timestamp - x.TaskEndedAt!.Value).TotalSeconds).FirstOrDefault(),
        result = !group.Any(x => x.Timestamp - x.TaskEndedAt >= TimeSpan.FromSeconds(30)) ? "UNVERIFIED_SHORT_OBSERVATION"
            : group.Any(x => x.Timestamp - x.TaskEndedAt <= TimeSpan.FromSeconds(30) && x.PrivateBytes <= 20_000_000 && x.CoverageComplete) ? "PASS_OBSERVED_RETURN" : "FAIL"
    }),
    limitations = new[] { "Sampled peaks are not absolute maximum guarantees; sub-sample helpers/startup spikes may be missed; use ETW/WPR to close this gap.",
        "All same-executable alarm helpers and observed descendants are charged to application. DWM is separate system context, not fully attributed to the application.",
        "Private Bytes is not physical RAM. Summed working sets/GPU shared allocations can double count shared pages; report separately.",
        "GPU missing counters and private working set unsupported by the OS are null, not zero. Compare DWM/system idle baseline separately.",
        "Collector is external test instrumentation and is excluded from product totals. No GC.Collect/EmptyWorkingSet/restarts are applied." }
};
await File.WriteAllTextAsync(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
using (var csv = new StreamWriter(Path.Combine(output, "samples.csv")))
{
    await csv.WriteLineAsync("timestamp,scenario,phase,private_bytes,private_working_set,total_working_set,cpu_seconds,cpu_normalized_percent,handles,threads,ollama_private_bytes,combined_private_bytes,gpu_dedicated,gpu_shared,gpu_percent,coverage_complete");
    foreach (var x in samples) await csv.WriteLineAsync(FormattableString.Invariant($"{x.Timestamp:O},{x.Scenario},{x.Phase},{x.PrivateBytes},{x.PrivateWorkingSet},{x.WorkingSet},{x.CpuSeconds},{x.CpuNormalizedPercent},{x.Handles},{x.Threads},{x.OllamaPrivateBytes},{x.PrivateBytes + x.OllamaPrivateBytes},{x.GpuDedicated},{x.GpuShared},{x.GpuPercent},{x.CoverageComplete}"));
}
Console.WriteLine(Path.Combine(output, "summary.json")); return 0;

static long? SumNullable(IEnumerable<long?> source) { var values = source.ToArray(); return values.Any(x => x == null) ? null : values.Sum(x => x ?? 0); }
static object Stats(IEnumerable<double> source) { var values = source.Order().ToArray(); return new { count = values.Length, average = values.Length > 0 ? (double?)values.Average() : null, p95 = values.Length > 0 ? (double?)values[(int)Math.Ceiling(values.Length * .95) - 1] : null, peak = values.Length > 0 ? (double?)values[^1] : null }; }
record ProcessRow(int Pid, int ParentPid, string Name, string Family, long PrivateBytes, long? PrivateWorkingSet, long WorkingSet, double CpuSeconds, int Handles, int Threads, DateTime StartedUtc);
record Sample(DateTimeOffset Timestamp, string Scenario, string Phase, string? TaskId, DateTimeOffset? TaskEndedAt, long PrivateBytes, long? PrivateWorkingSet, long WorkingSet, double CpuSeconds, double CpuNormalizedPercent, int Handles, int Threads, long OllamaPrivateBytes, long? OllamaPrivateWorkingSet, long OllamaWorkingSet, long? GpuDedicated, long? GpuShared, long? OllamaGpuDedicated, long? OllamaGpuShared, double? GpuPercent, bool CoverageComplete, double IntervalSeconds);
