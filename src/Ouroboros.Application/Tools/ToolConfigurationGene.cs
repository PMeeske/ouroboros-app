namespace Ouroboros.Application.Tools;

/// <summary>
/// Gene type for tool configuration evolution.
/// </summary>
public sealed record ToolConfigurationGene(string Key, string? Value);