namespace Ouroboros.Application.Mcp;

/// <summary>
/// Information about an MCP tool.
/// </summary>
public record McpToolInfo
{
    /// <summary>
    /// Gets or initializes the tool name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets or initializes the tool description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets or initializes the JSON schema for the tool's input.
    /// </summary>
    public required string InputSchema { get; init; }
}