// <copyright file="NullEngineImplementations.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

using System.Collections.Immutable;
using Ouroboros.Abstractions;
using Ouroboros.Core.Learning;
using Ouroboros.Core.Reasoning;
using Ouroboros.Domain.MetaLearning;
using Ouroboros.Domain.MultiAgent;
using Ouroboros.Pipeline.Memory;
using Ouroboros.Pipeline.Verification;
using Ouroboros.Tools.MeTTa;

// Type aliases to disambiguate types that exist in multiple namespaces
using MetaAIPlan = Ouroboros.Agent.MetaAI.Plan;
using VerificationPlan = Ouroboros.Pipeline.Verification.Plan;
using EmbodiedPlan = Ouroboros.Domain.Embodied.Plan;
using MemoryExecutionContext = Ouroboros.Pipeline.Memory.PipelineExecutionContext;
using MetaAIExecutionTrace = Ouroboros.Agent.MetaAI.ExecutionTrace;
using MetaAIHypothesis = Ouroboros.Tools.MeTTa.Hypothesis;
using MetaAIPlanExecutionResult = Ouroboros.Agent.MetaAI.PlanExecutionResult;
using DomainTaskAssignment = Ouroboros.Domain.MultiAgent.TaskAssignment;
using DomainSynthesisTask = Ouroboros.Domain.MetaLearning.SynthesisTask;
using DomainMetaLearningConfig = Ouroboros.Domain.MetaLearning.MetaLearningConfig;

// ────────────────────────────────────────────────────────────────────────────
//  No-op fallback implementations for IOuroborosCore engine interfaces.
//  Used when runtime dependencies (Qdrant, IEmbeddingModel, IMeTTaEngine, etc.)
//  are unavailable. Every method returns a safe default so the DI container
//  can always resolve IOuroborosCore without throwing.
// ────────────────────────────────────────────────────────────────────────────

internal sealed class NullEpisodicMemoryEngine : IEpisodicMemoryEngine
{
    public static readonly NullEpisodicMemoryEngine Instance = new();
    private static readonly string Unavailable = "Episodic memory unavailable";

    public Task<Result<EpisodeId, string>> StoreEpisodeAsync(
        PipelineBranch branch, MemoryExecutionContext context, Outcome result,
        ImmutableDictionary<string, object> metadata, CancellationToken ct = default)
        => Task.FromResult(Result<EpisodeId, string>.Failure(Unavailable));

    public Task<Result<ImmutableList<Episode>, string>> RetrieveSimilarEpisodesAsync(
        string query, int topK = 5, double minSimilarity = 0.7, CancellationToken ct = default)
        => Task.FromResult(Result<ImmutableList<Episode>, string>.Success(ImmutableList<Episode>.Empty));

    public Task<Result<Unit, string>> ConsolidateMemoriesAsync(
        TimeSpan olderThan, ConsolidationStrategy strategy, CancellationToken ct = default)
        => Task.FromResult(Result<Unit, string>.Success(Unit.Value));

    public Task<Result<VerificationPlan, string>> PlanWithExperienceAsync(
        string goal, ImmutableList<Episode> relevantEpisodes, CancellationToken ct = default)
        => Task.FromResult(Result<VerificationPlan, string>.Failure(Unavailable));
}

internal sealed class NullAdapterLearningEngine : IAdapterLearningEngine
{
    public static readonly NullAdapterLearningEngine Instance = new();
    private static readonly string Unavailable = "Adapter learning unavailable";

    public Task<Result<AdapterId, string>> CreateAdapterAsync(
        string taskName, AdapterConfig config, CancellationToken ct = default)
        => Task.FromResult(Result<AdapterId, string>.Failure(Unavailable));

    public Task<Result<Unit, string>> TrainAdapterAsync(
        AdapterId adapterId, List<TrainingExample> examples, TrainingConfig config, CancellationToken ct = default)
        => Task.FromResult(Result<Unit, string>.Failure(Unavailable));

    public Task<Result<Unit, string>> MergeAdaptersAsync(
        List<AdapterId> adapters, MergeStrategy strategy, CancellationToken ct = default)
        => Task.FromResult(Result<Unit, string>.Failure(Unavailable));

    public Task<Result<string, string>> GenerateWithAdapterAsync(
        string prompt, AdapterId? adapterId = null, CancellationToken ct = default)
        => Task.FromResult(Result<string, string>.Failure(Unavailable));

    public Task<Result<Unit, string>> LearnFromFeedbackAsync(
        string prompt, string generation, FeedbackSignal feedback, AdapterId adapterId, CancellationToken ct = default)
        => Task.FromResult(Result<Unit, string>.Failure(Unavailable));
}

internal sealed class NullAdvancedMeTTaEngine : IAdvancedMeTTaEngine
{
    public static readonly NullAdvancedMeTTaEngine Instance = new();
    private static readonly string Unavailable = "MeTTa engine unavailable";

    // IMeTTaEngine base methods
    public Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
        => Task.FromResult(Result<string, string>.Failure(Unavailable));

    public Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
        => Task.FromResult(Result<Unit, string>.Failure(Unavailable));

    public Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
        => Task.FromResult(Result<string, string>.Failure(Unavailable));

    public Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
        => Task.FromResult(Result<bool, string>.Failure(Unavailable));

    public Task<Result<Unit, string>> ResetAsync(CancellationToken ct = default)
        => Task.FromResult(Result<Unit, string>.Success(Unit.Value));

    public void Dispose() { }

    // IAdvancedMeTTaEngine methods
    public Task<Result<List<Rule>, string>> InduceRulesAsync(
        List<Fact> observations, InductionStrategy strategy, CancellationToken ct = default)
        => Task.FromResult(Result<List<Rule>, string>.Success(new List<Rule>()));

    public Task<Result<ProofTrace, string>> ProveTheoremAsync(
        string theorem, List<string> axioms, ProofStrategy strategy, CancellationToken ct = default)
        => Task.FromResult(Result<ProofTrace, string>.Failure(Unavailable));

    public Task<Result<List<MetaAIHypothesis>, string>> GenerateHypothesesAsync(
        string observation, List<string> backgroundKnowledge, CancellationToken ct = default)
        => Task.FromResult(Result<List<MetaAIHypothesis>, string>.Success(new List<MetaAIHypothesis>()));

    public Task<Result<TypedAtom, string>> InferTypeAsync(
        string atom, TypeContext context, CancellationToken ct = default)
        => Task.FromResult(Result<TypedAtom, string>.Failure(Unavailable));

    public Task<Result<List<Fact>, string>> ForwardChainAsync(
        List<Rule> rules, List<Fact> facts, int maxSteps = 10, CancellationToken ct = default)
        => Task.FromResult(Result<List<Fact>, string>.Success(new List<Fact>()));

    public Task<Result<List<Fact>, string>> BackwardChainAsync(
        Fact goal, List<Rule> rules, List<Fact> knownFacts, CancellationToken ct = default)
        => Task.FromResult(Result<List<Fact>, string>.Success(new List<Fact>()));
}

internal sealed class NullHierarchicalPlanner : Ouroboros.Agent.MetaAI.IHierarchicalPlanner
{
    public static readonly NullHierarchicalPlanner Instance = new();
    private static readonly string Unavailable = "Hierarchical planner unavailable";

    public Task<Result<Ouroboros.Agent.MetaAI.HierarchicalPlan, string>> CreateHierarchicalPlanAsync(
        string goal, Dictionary<string, object>? context = null,
        Ouroboros.Agent.MetaAI.HierarchicalPlanningConfig? config = null, CancellationToken ct = default)
        => Task.FromResult(Result<Ouroboros.Agent.MetaAI.HierarchicalPlan, string>.Failure(Unavailable));

    public Task<Result<MetaAIPlanExecutionResult, string>> ExecuteHierarchicalAsync(
        Ouroboros.Agent.MetaAI.HierarchicalPlan plan, CancellationToken ct = default)
        => Task.FromResult(Result<MetaAIPlanExecutionResult, string>.Failure(Unavailable));

    public Task<Result<Ouroboros.Agent.MetaAI.HtnHierarchicalPlan, string>> PlanHierarchicalAsync(
        string goal, Dictionary<string, Ouroboros.Agent.MetaAI.TaskDecomposition> taskNetwork, CancellationToken ct = default)
        => Task.FromResult(Result<Ouroboros.Agent.MetaAI.HtnHierarchicalPlan, string>.Failure(Unavailable));

    public Task<Result<Ouroboros.Agent.MetaAI.TemporalPlan, string>> PlanWithConstraintsAsync(
        string goal, List<Ouroboros.Agent.MetaAI.TemporalConstraint> constraints, CancellationToken ct = default)
        => Task.FromResult(Result<Ouroboros.Agent.MetaAI.TemporalPlan, string>.Failure(Unavailable));

    public Task<Result<MetaAIPlan, string>> RepairPlanAsync(
        MetaAIPlan brokenPlan, MetaAIExecutionTrace trace,
        Ouroboros.Agent.MetaAI.RepairStrategy strategy, CancellationToken ct = default)
        => Task.FromResult(Result<MetaAIPlan, string>.Failure(Unavailable));

    public Task<Result<string, string>> ExplainPlanAsync(
        MetaAIPlan plan, Ouroboros.Agent.MetaAI.ExplanationLevel level, CancellationToken ct = default)
        => Task.FromResult(Result<string, string>.Failure(Unavailable));
}

internal sealed class NullMultiAgentCoordinator : IMultiAgentCoordinator
{
    public static readonly NullMultiAgentCoordinator Instance = new();
    private static readonly string Unavailable = "Multi-agent coordinator unavailable";

    public Task<Result<Unit, string>> BroadcastMessageAsync(
        Message message, AgentGroup recipients, CancellationToken ct = default)
        => Task.FromResult(Result<Unit, string>.Failure(Unavailable));

    public Task<Result<Dictionary<AgentId, DomainTaskAssignment>, string>> AllocateTasksAsync(
        string goal, List<AgentCapabilities> availableAgents, AllocationStrategy strategy, CancellationToken ct = default)
        => Task.FromResult(Result<Dictionary<AgentId, DomainTaskAssignment>, string>.Failure(Unavailable));

    public Task<Result<Decision, string>> ReachConsensusAsync(
        string proposal, List<AgentId> voters, ConsensusProtocol protocol, CancellationToken ct = default)
        => Task.FromResult(Result<Decision, string>.Failure(Unavailable));

    public Task<Result<Unit, string>> SynchronizeKnowledgeAsync(
        List<AgentId> agents, KnowledgeSyncStrategy strategy, CancellationToken ct = default)
        => Task.FromResult(Result<Unit, string>.Failure(Unavailable));

    public Task<Result<CollaborativePlan, string>> PlanCollaborativelyAsync(
        string goal, List<AgentId> participants, CancellationToken ct = default)
        => Task.FromResult(Result<CollaborativePlan, string>.Failure(Unavailable));
}

internal sealed class NullMetaLearningEngine : IMetaLearningEngine
{
    public static readonly NullMetaLearningEngine Instance = new();
    private static readonly string Unavailable = "Meta-learning engine unavailable";

    public Task<Result<MetaModel, string>> MetaTrainAsync(
        List<TaskFamily> taskFamilies, DomainMetaLearningConfig config, CancellationToken ct = default)
        => Task.FromResult(Result<MetaModel, string>.Failure(Unavailable));

    public Task<Result<AdaptedModel, string>> AdaptToTaskAsync(
        MetaModel metaModel, List<Example> fewShotExamples, int adaptationSteps, CancellationToken ct = default)
        => Task.FromResult(Result<AdaptedModel, string>.Failure(Unavailable));

    public Task<Result<double, string>> ComputeTaskSimilarityAsync(
        DomainSynthesisTask taskA, DomainSynthesisTask taskB, MetaModel metaModel, CancellationToken ct = default)
        => Task.FromResult(Result<double, string>.Failure(Unavailable));

    public Task<Result<TaskEmbedding, string>> EmbedTaskAsync(
        DomainSynthesisTask task, MetaModel metaModel, CancellationToken ct = default)
        => Task.FromResult(Result<TaskEmbedding, string>.Failure(Unavailable));
}

internal sealed class NullEmbodiedAgent : Ouroboros.Domain.Embodied.IEmbodiedAgent
{
    public static readonly NullEmbodiedAgent Instance = new();
    private static readonly string Unavailable = "Embodied agent unavailable";

    public Task<Result<Unit, string>> InitializeInEnvironmentAsync(
        Ouroboros.Domain.Embodied.EnvironmentConfig environment, CancellationToken ct = default)
        => Task.FromResult(Result<Unit, string>.Failure(Unavailable));

    public Task<Result<Ouroboros.Domain.Embodied.SensorState, string>> PerceiveAsync(CancellationToken ct = default)
        => Task.FromResult(Result<Ouroboros.Domain.Embodied.SensorState, string>.Failure(Unavailable));

    public Task<Result<Ouroboros.Domain.Embodied.ActionResult, string>> ActAsync(
        Ouroboros.Domain.Embodied.EmbodiedAction action, CancellationToken ct = default)
        => Task.FromResult(Result<Ouroboros.Domain.Embodied.ActionResult, string>.Failure(Unavailable));

    public Task<Result<Unit, string>> LearnFromExperienceAsync(
        IReadOnlyList<Ouroboros.Domain.Embodied.EmbodiedTransition> transitions, CancellationToken ct = default)
        => Task.FromResult(Result<Unit, string>.Failure(Unavailable));

    public Task<Result<EmbodiedPlan, string>> PlanEmbodiedAsync(
        string goal, Ouroboros.Domain.Embodied.SensorState currentState, CancellationToken ct = default)
        => Task.FromResult(Result<EmbodiedPlan, string>.Failure(Unavailable));
}
