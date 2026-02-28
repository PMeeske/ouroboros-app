// <copyright file="ImmersiveCognitiveContext.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.CLI.Commands;

/// <summary>
/// Groups cognitive subsystem fields for ImmersiveMode:
/// ethics, cognitive physics, phi calculation, episodic memory,
/// metacognition, neural-symbolic bridge, causal reasoning, and curiosity.
/// </summary>
internal sealed class ImmersiveCognitiveContext
{
    public Ouroboros.Core.Ethics.IEthicsFramework? Ethics { get; set; }

    public Ouroboros.Core.CognitivePhysics.CognitivePhysicsEngine? CogPhysics { get; set; }

    public Ouroboros.Core.CognitivePhysics.CognitiveState CogState { get; set; }
        = Ouroboros.Core.CognitivePhysics.CognitiveState.Create("general");

    public Ouroboros.Providers.IITPhiCalculator PhiCalc { get; set; } = new();

    public string LastTopic { get; set; } = "general";

    public int ResponseCount { get; set; }

    public Ouroboros.Pipeline.Memory.IEpisodicMemoryEngine? EpisodicMemory { get; set; }

    public Ouroboros.Pipeline.Metacognition.MetacognitiveReasoner Metacognition { get; } = new();

    public Ouroboros.Agent.NeuralSymbolic.INeuralSymbolicBridge? NeuralSymbolicBridge { get; set; }

    public Ouroboros.Core.Reasoning.ICausalReasoningEngine CausalReasoning { get; set; }
        = new Ouroboros.Core.Reasoning.CausalReasoningEngine();

    public Ouroboros.Agent.MetaAI.ICuriosityEngine? CuriosityEngine { get; set; }
}
