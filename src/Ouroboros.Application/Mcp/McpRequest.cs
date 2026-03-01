using System.Text.Json.Serialization;

namespace Ouroboros.Application.Mcp;

/// <summary>
/// An MCP JSON-RPC request.
/// </summary>
public record McpRequest
{
    /// <summary>
    /// Gets or sets the JSON-RPC version.
    /// </summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    /// <summary>
    /// Gets or initializes the request ID (null for notifications).
    /// </summary>
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Id { get; init; }

    /// <summary>
    /// Gets or initializes the method name.
    /// </summary>
    [JsonPropertyName("method")]
    public required string Method { get; init; }

    /// <summary>
    /// Gets or initializes the parameters.
    /// </summary>
    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Params { get; init; }
}