// <copyright file="ImmersiveLearningContext.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.CLI.Commands;

using Ouroboros.Agent;
using Ouroboros.Application.Personality.Consciousness;
using Ouroboros.Core.DistinctionLearning;
using Ouroboros.Domain.DistinctionLearning;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

/// <summary>
/// Groups learning subsystem fields for ImmersiveMode:
/// distinction learning, dream/DreamCoder, model orchestration,
/// divide-and-conquer, and sovereignty gate.
/// </summary>
internal sealed class ImmersiveLearningContext
{
    public IDistinctionLearner? DistinctionLearner { get; set; }

    public ConsciousnessDream? Dream { get; set; }

    public DistinctionState CurrentDistinctionState { get; set; } = DistinctionState.Initial();

    public OrchestratedChatModel? OrchestratedModel { get; set; }

    public DivideAndConquerOrchestrator? DivideAndConquer { get; set; }

    public IChatCompletionModel? BaseModel { get; set; }

    public Ouroboros.CLI.Sovereignty.PersonaSovereigntyGate? SovereigntyGate { get; set; }
}
