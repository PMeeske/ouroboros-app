namespace Ouroboros.Application.Mcp;

/// <summary>
/// Result from calling an MCP tool.
/// </summary>
public record McpToolResult
{
    /// <summary>
    /// Gets or initializes whether the result is an error.
    /// </summary>
    public bool IsError { get; init; }

    /// <summary>
    /// Gets or initializes the content of the result.
    /// </summary>
    public required string Content { get; init; }
}