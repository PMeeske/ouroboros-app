// <copyright file="AutonomousTools.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

/// <summary>
/// Registry and factory for autonomous mode tools.
/// Individual tool implementations live in the <c>Tools/Autonomous/</c> directory.
/// </summary>
public static class AutonomousTools
{
    /// <summary>
    /// Default shared context used when no explicit context is provided.
    /// Consumers should prefer injecting <see cref="IAutonomousToolContext"/> directly.
    /// </summary>
    [Obsolete("Use IAutonomousToolContext from DI")]
    public static IAutonomousToolContext DefaultContext { get; set; } = new AutonomousToolContext();

    /// <summary>
    /// Legacy autonomous coordinator reference.
    /// Delegates to <see cref="DefaultContext"/>. Prefer using <see cref="IAutonomousToolContext.Coordinator"/> directly.
    /// </summary>
    [Obsolete("Use IAutonomousToolContext from DI")]
    public static AutonomousCoordinator? LegacyCoordinator
    {
        get => DefaultContext.Coordinator;
        set => DefaultContext.Coordinator = value;
    }

    /// <summary>
    /// Gets all autonomous tools using the specified context.
    /// </summary>
    public static IEnumerable<ITool> GetAllTools(IAutonomousToolContext context)
    {
        yield return new GetAutonomousStatusTool(context);
        yield return new ListPendingIntentionsTool(context);
        yield return new ApproveIntentionTool(context);
        yield return new RejectIntentionTool(context);
        yield return new ProposeIntentionTool(context);
        yield return new GetNetworkStatusTool(context);
        yield return new SendNeuronMessageTool(context);
        yield return new ToggleAutonomousModeTool(context);
        yield return new InjectGoalTool(context);
        yield return new SearchNeuronHistoryTool(context);
        yield return new FirecrawlScrapeTool();
        yield return new FirecrawlResearchTool();
        yield return new LocalWebScrapeTool();
        yield return new CliDslTool(context);

        // Limitation-busting tools
        yield return new VerifyClaimTool(context);
        yield return new ReasoningChainTool(context);
        yield return new EpisodicMemoryTool(context);
        yield return new ParallelToolsTool(context);
        yield return new CompressContextTool(context);
        yield return new ParallelMeTTaThinkTool(context);
        yield return new SelfDoubtTool(context);
        yield return new OuroborosMeTTaTool(context);
    }

    /// <summary>
    /// Gets all autonomous tools using the <see cref="DefaultContext"/>.
    /// </summary>
    public static IEnumerable<ITool> GetAllTools() => GetAllTools(DefaultContext);

    /// <summary>
    /// Adds autonomous tools to a registry using the specified context.
    /// </summary>
    public static ToolRegistry WithAutonomousTools(this ToolRegistry registry, IAutonomousToolContext context)
    {
        foreach (var tool in GetAllTools(context))
        {
            registry = registry.WithTool(tool);
        }
        return registry;
    }

    /// <summary>
    /// Adds autonomous tools to a registry using the <see cref="DefaultContext"/>.
    /// </summary>
    public static ToolRegistry WithAutonomousTools(this ToolRegistry registry)
        => registry.WithAutonomousTools(DefaultContext);
}
