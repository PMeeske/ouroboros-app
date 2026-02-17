using System.Text.Json.Serialization;

namespace Ouroboros.Application.CodeGeneration;

/// <summary>
/// MCP response with available tools.
/// </summary>
public class McpResponse
{
    [JsonPropertyName("tools")]
    public List<McpTool> Tools { get; set; } = new List<McpTool>();
}