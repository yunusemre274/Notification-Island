using System.IO;
using System.Text.Json;

namespace NI.Agent;

public sealed class ToolRegistry
{
    private readonly Dictionary<string, ToolRegistration> _tools = new(StringComparer.Ordinal);
    public IReadOnlyList<ToolDefinition> Definitions => _tools.Values.Select(x => x.Definition).ToArray();
    public void Add(ToolRegistration tool)
    {
        if (!_tools.TryAdd(tool.Definition.Name, tool)) throw new InvalidOperationException("Yinelenen araç adı.");
    }
    public bool TryGet(string name, out ToolRegistration registration) => _tools.TryGetValue(name, out registration!);
    public static JsonElement Schema(string[] required, object properties) => AgentJson.Element(new
    { type = "object", required, properties, additionalProperties = false });
    public static object String(string description, string[]? choices = null) => choices == null
        ? new { type = "string", description } : (object)new { type = "string", description, @enum = choices };
    public static ToolRegistration Create(string name, string description, string scope, ToolRisk risk, JsonElement schema,
        Func<JsonElement, ToolContext, CancellationToken, Task<ToolResult>> execute, Func<JsonElement, string>? approval = null) => new(
            new(name, 1, description, schema, scope, risk, "Cancellation between verified steps; tool timeout; no abandoned mutation task.",
                risk == ToolRisk.ReadOnly ? "Read-only" : "Durable operation ID; reconcile uncertain effects before retry; not exactly-once.",
                scope == "files" ? "Journal-based, collision-safe undo where supported" : "Explicit update/cancel; no automatic rollback"), execute, approval);
}
