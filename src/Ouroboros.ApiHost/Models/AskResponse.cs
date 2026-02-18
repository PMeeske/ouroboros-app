namespace Ouroboros.ApiHost.Models;

/// <summary>
/// Response model for ask endpoint
/// </summary>
public sealed record AskResponse
{
    /// <summary>
    /// The generated answer text
    /// </summary>
    public required string Answer { get; init; }

    /// <summary>
    /// The model used to generate the answer (optional)
    /// </summary>
    public string? Model { get; init; }
}