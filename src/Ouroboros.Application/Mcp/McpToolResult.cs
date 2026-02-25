namespace Ouroboros.Application.Mcp;

/// <summary>
/// Result from calling an MCP tool.
/// </summary>
public record McpToolResult
{
    /// <summary>
    /// Gets or sets whether the result is an error.
    /// </summary>
    public bool IsError { get; set; }

    /// <summary>
    /// Gets or sets the content of the result.
    /// </summary>
    public required string Content { get; set; }
}