// <copyright file="SystemAccessTools.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

using Ouroboros.Application.Services;
using Ouroboros.Application.Tools.SystemTools;
using Ouroboros.Tools;

/// <summary>
/// Provides comprehensive system access tools for Ouroboros.
/// Enables file system, process, registry, and system information access.
/// Individual tool implementations live in the <c>System/</c> subdirectory.
/// </summary>
public static class SystemAccessTools
{
    /// <summary>
    /// Shared self-indexer instance for indexing tools.
    /// </summary>
    public static QdrantSelfIndexer? SharedIndexer { get; set; }

    /// <summary>
    /// Shared self-persistence instance.
    /// </summary>
    public static SelfPersistence? SharedPersistence { get; set; }

    /// <summary>
    /// Shared autonomous mind reference.
    /// </summary>
    public static AutonomousMind? SharedMind { get; set; }

    /// <summary>
    /// Creates all system access tools.
    /// </summary>
    public static IEnumerable<ITool> CreateAllTools()
    {
        // File system tools
        yield return new FileSystemTool();
        yield return new DirectoryListTool();
        yield return new FileReadTool();
        yield return new FileWriteTool();
        yield return new FileSearchTool();
        yield return new FileIndexTool();
        yield return new SearchIndexedContentTool();

        // Self-introspection tools
        yield return new SearchMyCodeTool();
        yield return new ReadMyFileTool();

        // Self-modification tools (true self-evolution!)
        yield return new ModifyMyCodeTool();
        yield return new CreateNewToolTool();
        yield return new RebuildSelfTool();
        yield return new ViewModificationHistoryTool();
        yield return new RevertModificationTool();

        // Self-persistence tools
        yield return new PersistSelfTool();
        yield return new RestoreSelfTool();
        yield return new SearchMyThoughtsTool();
        yield return new PersistenceStatsTool();

        // Service discovery — Scrutor + IServiceProvider runtime invocation
        yield return new ServiceDiscoveryTool();

        // System tools
        yield return new ProcessListTool();
        yield return new ProcessStartTool();
        yield return new ProcessKillTool();
        yield return new SystemInfoTool();
        yield return new EnvironmentTool();
        yield return new PowerShellTool();
        yield return new ClipboardTool();
        yield return new NetworkInfoTool();
        yield return new DiskInfoTool();
    }
}
