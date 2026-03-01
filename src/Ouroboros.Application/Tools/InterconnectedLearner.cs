// <copyright file="InterconnectedLearner.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

using System.Collections.Concurrent;
using System.Text;
using Ouroboros.Abstractions.Agent;  // For interfaces: ISkillRegistry, IMemoryStore, ISafetyGuard, IUncertaintyRouter
using Ouroboros.Agent.MetaAI;  // For concrete implementations and other types
using Ouroboros.Genetic.Abstractions;
using Ouroboros.Genetic.Core;
using Ouroboros.Providers;
using Ouroboros.Tools;
using Ouroboros.Tools.MeTTa;

/// <summary>
/// Intelligent interconnected learning system that bridges tools, skills, and pipelines
/// using LLM reasoning, genetic algorithm optimization, embeddings, and MeTTa symbolic reasoning.
///
/// This system:
/// 1. Records all tool, skill, and pipeline executions
/// 2. Uses LLM to analyze patterns and suggest optimizations
/// 3. Uses genetic algorithms to evolve optimal action sequences
/// 4. Uses embeddings to find semantically similar patterns
/// 5. Uses MeTTa for symbolic reasoning about relationships
/// 6. Learns and adapts over time to improve suggestions
/// </summary>
/// <remarks>
/// This class is split into partial files:
/// - InterconnectedLearner.cs (this file): Core fields, constructor, execution recording, disposal
/// - InterconnectedLearner.Reasoning.cs: MeTTa reasoning, LLM analysis, embedding similarity
/// - InterconnectedLearner.Evolution.cs: Genetic algorithm, pattern learning, suggestions, helpers
/// </remarks>
public sealed partial class InterconnectedLearner : IAsyncDisposable
{
    private readonly DynamicToolFactory _toolFactory;
    private readonly Ouroboros.Agent.MetaAI.ISkillRegistry? _skillRegistry;
    private readonly IMeTTaEngine _mettaEngine;
    private readonly IEmbeddingModel? _embeddingModel;
    private readonly ToolAwareChatModel? _llm;

    // Execution history for pattern learning
    private readonly ConcurrentQueue<ExecutionRecord> _executionLog = new();
    private readonly ConcurrentDictionary<string, InterconnectedPattern> _patterns = new();
    private readonly ConcurrentDictionary<string, List<string>> _conceptGraph = new();

    // Statistics
    private int _totalToolExecutions;
    private int _totalSkillExecutions;
    private int _totalPipelineExecutions;
    private int _successfulExecutions;

    private const int MaxExecutionHistory = 1000;

    /// <summary>
    /// Initializes a new instance of the <see cref="InterconnectedLearner"/> class.
    /// </summary>
    public InterconnectedLearner(
        DynamicToolFactory toolFactory,
        Ouroboros.Agent.MetaAI.ISkillRegistry? skillRegistry,
        IMeTTaEngine mettaEngine,
        IEmbeddingModel? embeddingModel = null,
        ToolAwareChatModel? llm = null)
    {
        _toolFactory = toolFactory ?? throw new ArgumentNullException(nameof(toolFactory));
        _skillRegistry = skillRegistry;
        _mettaEngine = mettaEngine ?? throw new ArgumentNullException(nameof(mettaEngine));
        _embeddingModel = embeddingModel;
        _llm = llm;
    }

    #region Execution Recording

    /// <summary>
    /// Records a tool execution for pattern learning.
    /// </summary>
    public async Task RecordToolExecutionAsync(
        string toolName,
        string input,
        string output,
        bool success,
        TimeSpan duration,
        CancellationToken ct = default)
    {
        var record = new ExecutionRecord(
            Id: Guid.NewGuid().ToString("N"),
            ExecutionType: "tool",
            Name: toolName,
            Input: input,
            Output: output,
            Success: success,
            Duration: duration,
            Timestamp: DateTime.UtcNow,
            Metadata: new Dictionary<string, string>());

        EnqueueExecution(record);
        Interlocked.Increment(ref _totalToolExecutions);
        if (success) Interlocked.Increment(ref _successfulExecutions);

        // Add to MeTTa knowledge base
        await AddToMeTTaKnowledgeAsync(record, ct);

        // Update concept graph
        UpdateConceptGraph(record);

        // Try to learn patterns periodically
        if (_executionLog.Count % 10 == 0)
        {
            await TryLearnPatternsAsync(ct);
        }
    }

    /// <summary>
    /// Records a skill execution for pattern learning.
    /// </summary>
    public async Task RecordSkillExecutionAsync(
        string skillName,
        string input,
        string output,
        bool success,
        CancellationToken ct = default)
    {
        var record = new ExecutionRecord(
            Id: Guid.NewGuid().ToString("N"),
            ExecutionType: "skill",
            Name: skillName,
            Input: input,
            Output: output,
            Success: success,
            Duration: TimeSpan.Zero,
            Timestamp: DateTime.UtcNow,
            Metadata: new Dictionary<string, string>());

        EnqueueExecution(record);
        Interlocked.Increment(ref _totalSkillExecutions);
        if (success) Interlocked.Increment(ref _successfulExecutions);

        await AddToMeTTaKnowledgeAsync(record, ct);
        UpdateConceptGraph(record);

        if (_executionLog.Count % 10 == 0)
        {
            await TryLearnPatternsAsync(ct);
        }
    }

    /// <summary>
    /// Records a pipeline execution for pattern learning.
    /// </summary>
    public async Task RecordPipelineExecutionAsync(
        string pipelineName,
        string input,
        string output,
        bool success,
        TimeSpan duration,
        CancellationToken ct = default)
    {
        var record = new ExecutionRecord(
            Id: Guid.NewGuid().ToString("N"),
            ExecutionType: "pipeline",
            Name: pipelineName,
            Input: input,
            Output: output,
            Success: success,
            Duration: duration,
            Timestamp: DateTime.UtcNow,
            Metadata: new Dictionary<string, string>());

        EnqueueExecution(record);
        Interlocked.Increment(ref _totalPipelineExecutions);
        if (success) Interlocked.Increment(ref _successfulExecutions);

        await AddToMeTTaKnowledgeAsync(record, ct);
        UpdateConceptGraph(record);
    }

    private void EnqueueExecution(ExecutionRecord record)
    {
        _executionLog.Enqueue(record);

        // Prune old records to avoid memory bloat
        while (_executionLog.Count > MaxExecutionHistory && _executionLog.TryDequeue(out _))
        {
        }
    }

    #endregion

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _executionLog.Clear();
        _patterns.Clear();
        _conceptGraph.Clear();
        return ValueTask.CompletedTask;
    }
}

#region Supporting Types

#endregion

#region Genetic Algorithm Support Types

#endregion
