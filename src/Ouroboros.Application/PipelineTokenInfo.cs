using System.Reflection;

namespace Ouroboros.Application;

/// <summary>
/// Information about a discovered pipeline token for dynamic discovery.
/// </summary>
public record PipelineTokenInfo(
    string PrimaryName,
    string[] Aliases,
    string SourceClass,
    string Description,
    MethodInfo Method
);