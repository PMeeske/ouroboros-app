using System.Text.Json.Serialization;

namespace Ouroboros.Application.CodeGeneration;

/// <summary>
/// Result of MCP tool execution.
/// </summary>
public class McpToolResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public object? Data { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}