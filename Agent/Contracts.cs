using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NI.Agent;

public enum AgentTaskState { Pending, Planning, Running, AwaitingApproval, Completed, PartiallyCompleted, Failed, Cancelled }
public enum ToolRisk { ReadOnly, Prepare, Reversible, SystemChange }
public sealed record ToolResult(bool Success, string ErrorCode, string UserMessage, string OperationId,
    object? Data = null, int VerifiedChanges = 0)
{
    public static ToolResult Fail(string code, string message, string id = "") => new(false, code, message, id);
}
public sealed record ToolCall(string Name, JsonElement Arguments);
public sealed record ModelTurn(string Content, IReadOnlyList<ToolCall> Calls, string? Thinking = null);
public sealed record ChatMessage(string Role, string Content, string? ToolName = null, IReadOnlyList<ToolCall>? Calls = null);
public sealed record ToolDefinition(string Name, int Version, string Description, JsonElement InputSchema,
    string Scope, ToolRisk Risk, string Cancellation, string Idempotency, string Undo);
public sealed record TaskSnapshot(Guid Id, AgentTaskState State, string Summary, DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt, int CompletedSteps, int VerifiedChanges, string? LastOperationId, string? ErrorCode = null, string? ModelRelease = null);
public sealed record ApprovalRequest(Guid TaskId, string PlanHash, string Summary);

public interface IAgentModel : IDisposable
{
    Task ValidateAsync(CancellationToken token);
    Task<ModelTurn> ChatAsync(IReadOnlyList<ChatMessage> messages, IReadOnlyList<ToolDefinition> tools, CancellationToken token);
    string ReleaseStatus => "Unverified";
    Task ReleaseAsync(CancellationToken token);
}
public sealed record ToolContext(Guid TaskId, string UserRequest, PermissionGrant Grant,
    Action<string, int, int>? Progress = null);
public sealed record ToolRegistration(ToolDefinition Definition,
    Func<JsonElement, ToolContext, CancellationToken, Task<ToolResult>> Execute,
    Func<JsonElement, string>? ApprovalSummary = null);
public static class AgentJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter() }, MaxDepth = 20 };
    public static JsonElement Element(object value) => JsonSerializer.SerializeToElement(value, Options);
}
