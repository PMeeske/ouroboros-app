namespace Ouroboros.Application.Mcp;

/// <summary>
/// Information about an MCP tool.
/// </summary>
public record McpToolInfo
{
    /// <summary>
    /// Gets or sets the tool name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the tool description.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Gets or sets the JSON schema for the tool's input.
    /// </summary>
    public required string InputSchema { get; set; }
}