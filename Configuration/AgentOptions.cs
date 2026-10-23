namespace NI.Agent;

public sealed class AgentOptions
{
    public int MaxTaskSeconds { get; set; } = 600;
    public int MaxTurns { get; set; } = 12;
    public int ModelTimeoutSeconds { get; set; } = 90;
    public int ToolTimeoutSeconds { get; set; } = 120;
    public int ContextTokens { get; set; } = 4096;
    public int MaxContextCharacters { get; set; } = 14000;
    public int MaxResponseBytes { get; set; } = 65536;
    public bool UnloadOwnedModelAfterTask { get; set; } = false;
    public bool ManagedServer { get; set; } = false;
    public string TimeZoneId { get; set; } = TimeZoneInfo.Local.Id;
    public void Validate()
    {
        MaxTaskSeconds = Math.Clamp(MaxTaskSeconds, 30, 1800);
        MaxTurns = Math.Clamp(MaxTurns, 1, 24);
        ModelTimeoutSeconds = Math.Clamp(ModelTimeoutSeconds, 5, 300);
        ToolTimeoutSeconds = Math.Clamp(ToolTimeoutSeconds, 5, 600);
        ContextTokens = Math.Clamp(ContextTokens, 2048, 8192);
        MaxContextCharacters = Math.Clamp(MaxContextCharacters, 4000, 20000);
        MaxResponseBytes = Math.Clamp(MaxResponseBytes, 8192, 131072);
        _ = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
    }
}
