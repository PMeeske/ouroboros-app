// <copyright file="ImmersiveToolContext.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.CLI.Commands;

using Ouroboros.Abstractions.Agent;
using Ouroboros.Application.Services;
using Ouroboros.Application.Tools;
using Ouroboros.Network;

/// <summary>
/// Groups tool subsystem fields for ImmersiveMode:
/// skill registry, dynamic tool factory, tool learner,
/// interconnected learner, self-indexer, conversation memory,
/// network state projector, and dynamic tools list.
/// </summary>
internal sealed class ImmersiveToolContext
{
    public ISkillRegistry? SkillRegistry { get; set; }

    public DynamicToolFactory? DynamicToolFactory { get; set; }

    public IntelligentToolLearner? ToolLearner { get; set; }

    public InterconnectedLearner? InterconnectedLearner { get; set; }

    public QdrantSelfIndexer? SelfIndexer { get; set; }

    public PersistentConversationMemory? ConversationMemory { get; set; }

    public PersistentNetworkStateProjector? NetworkStateProjector { get; set; }

    public ToolRegistry DynamicTools { get; set; } = new();
}
