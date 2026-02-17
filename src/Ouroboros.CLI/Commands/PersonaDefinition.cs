namespace Ouroboros.CLI.Commands;

/// <summary>
/// Persona definition with voice characteristics.
/// </summary>
public sealed record PersonaDefinition(
    string Name,
    string Voice,
    string[] Traits,
    string[] Moods,
    string CoreIdentity);