// <copyright file="GitReflectionTools.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Domain.SelfModification;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Tools for Git-based code reflection and self-modification.
/// These tools enable Ouroboros to analyze, understand, and modify its own source code.
/// Individual tool implementations are in GitReflection/*.cs partial class files.
/// </summary>
public static partial class GitReflectionTools
{
    private static GitReflectionService? _service;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets or initializes the Git reflection service.
    /// </summary>
    private static GitReflectionService GetService()
    {
        if (_service == null)
        {
            lock (_lock)
            {
                _service ??= new GitReflectionService();
            }
        }
        return _service;
    }

    /// <summary>
    /// Gets all Git reflection tools as a collection.
    /// Use this to add them to a ToolRegistry using WithTool().
    /// </summary>
    public static IEnumerable<ITool> GetAllTools()
    {
        yield return new GetCodebaseOverviewTool();
        yield return new AnalyzeFileTool();
        yield return new SearchCodeTool();
        yield return new ListSourceFilesTool();
        yield return new GitStatusTool();
        yield return new GitBranchTool();
        yield return new GitCommitTool();
        yield return new ProposeChangeTool();
        yield return new ApproveChangeTool();
        yield return new ApplyChangeTool();
        yield return new SelfModifyTool();
        yield return new GetModificationLogTool();
        yield return new ReflectOnCodeTool();
    }

    /// <summary>
    /// Adds all Git reflection tools to an existing registry.
    /// </summary>
    public static ToolRegistry WithGitReflectionTools(this ToolRegistry registry)
    {
        foreach (ITool tool in GetAllTools())
        {
            registry = registry.WithTool(tool);
        }
        return registry;
    }
}
