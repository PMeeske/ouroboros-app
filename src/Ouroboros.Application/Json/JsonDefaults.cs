using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ouroboros.Application.Json;

/// <summary>
/// Shared, pre-allocated <see cref="JsonSerializerOptions"/> instances.
/// <c>JsonSerializerOptions</c> is expensive to construct; caching avoids
/// repeated allocations on every serialize/deserialize call.
/// </summary>
public static class JsonDefaults
{
    /// <summary>
    /// Indented output with camelCase naming and null-skipping.
    /// Use for human-readable JSON exports and CLI output.
    /// </summary>
    public static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Compact output with camelCase naming and null-skipping.
    /// Use for API responses and wire-format payloads.
    /// </summary>
    public static readonly JsonSerializerOptions Compact = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Case-insensitive deserialization with null-skipping.
    /// Use when reading JSON from external sources.
    /// </summary>
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Indented output without any naming policy.
    /// Use for round-trip serialization where property names must match exactly.
    /// </summary>
    public static readonly JsonSerializerOptions IndentedExact = new()
    {
        WriteIndented = true,
    };
}
