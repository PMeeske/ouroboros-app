namespace Ouroboros.Application.Integration;

/// <summary>Options for reflection configuration.</summary>
public sealed record ReflectionOptions(
    bool EnableCodeReflection = true,
    bool EnablePerformanceReflection = true,
    int ReflectionDepth = 3);