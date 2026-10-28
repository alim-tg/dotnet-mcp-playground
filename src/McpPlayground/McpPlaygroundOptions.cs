namespace McpPlayground;

public sealed class McpPlaygroundOptions
{
    public string Title { get; set; } = "MCP Playground";

    public string? Description { get; set; }

    public string McpEndpoint { get; set; } = "/mcp";

    public string? AuthHeaderName { get; set; } = "Authorization";

    public string? AuthScheme { get; set; } = "Bearer";

    public McpPlaygroundKeyStorage KeyStorage { get; set; } = McpPlaygroundKeyStorage.Session;

    public string ProtocolVersion { get; set; } = "2025-06-18";

    public bool AllowAnonymous { get; set; } = true;
}

public enum McpPlaygroundKeyStorage
{
    None,
    Session,
    Local
}
