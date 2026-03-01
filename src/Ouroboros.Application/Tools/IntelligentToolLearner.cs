// <copyright file="IntelligentToolLearner.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

using System.Collections.Concurrent;
using System.Text.Json;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Core.Configuration;
using Ouroboros.Genetic.Abstractions;
using Ouroboros.Genetic.Core;
using Ouroboros.Providers;
using Ouroboros.Tools.MeTTa;
using Ouroboros.Tools;
using Qdrant.Client;
using Qdrant.Client.Grpc;

/// <summary>
/// Intelligent tool learning system that combines:
/// - MeTTa semantic reasoning for tool understanding and selection
/// - Genetic algorithm optimization for tool configurations
/// - Qdrant vector storage for persistent pattern learning
/// </summary>
public sealed partial class IntelligentToolLearner : IAsyncDisposable
{
    private readonly DynamicToolFactory _toolFactory;
    private readonly IMeTTaEngine _mettaEngine;
    private readonly MeTTaRepresentation _mettaRepresentation;
    private readonly IEmbeddingModel _embedding;
    private readonly QdrantClient _qdrantClient;
    private readonly ToolAwareChatModel _llm;
    private readonly ConcurrentDictionary<string, LearnedToolPattern> _patternCache = new();
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private bool _isInitialized;
    private int _vectorSize = 32; // Will be detected from embedding model

    private readonly string _collectionName;

    /// <summary>
    /// Initializes a new instance using the DI-provided client and collection registry.
    /// </summary>
    public IntelligentToolLearner(
        DynamicToolFactory toolFactory,
        IMeTTaEngine mettaEngine,
        IEmbeddingModel embedding,
        ToolAwareChatModel llm,
        QdrantClient qdrantClient,
        IQdrantCollectionRegistry registry)
    {
        _toolFactory = toolFactory ?? throw new ArgumentNullException(nameof(toolFactory));
        _mettaEngine = mettaEngine ?? throw new ArgumentNullException(nameof(mettaEngine));
        _embedding = embedding ?? throw new ArgumentNullException(nameof(embedding));
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _qdrantClient = qdrantClient ?? throw new ArgumentNullException(nameof(qdrantClient));
        ArgumentNullException.ThrowIfNull(registry);
        _mettaRepresentation = new MeTTaRepresentation(mettaEngine);
        _collectionName = registry.GetCollectionName(QdrantCollectionRole.ToolPatterns);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IntelligentToolLearner"/> class.
    /// </summary>
    [Obsolete("Use the constructor accepting QdrantClient + IQdrantCollectionRegistry from DI.")]
    public IntelligentToolLearner(
        DynamicToolFactory toolFactory,
        IMeTTaEngine mettaEngine,
        IEmbeddingModel embedding,
        ToolAwareChatModel llm,
        string qdrantUrl = Configuration.DefaultEndpoints.QdrantGrpc)
    {
        _toolFactory = toolFactory ?? throw new ArgumentNullException(nameof(toolFactory));
        _mettaEngine = mettaEngine ?? throw new ArgumentNullException(nameof(mettaEngine));
        _embedding = embedding ?? throw new ArgumentNullException(nameof(embedding));
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _mettaRepresentation = new MeTTaRepresentation(mettaEngine);
        _collectionName = "ouroboros_tool_patterns";

        var uri = new Uri(qdrantUrl);
        _qdrantClient = new QdrantClient(uri.Host, uri.Port > 0 ? uri.Port : 6334, uri.Scheme == "https");
    }

    /// <summary>
    /// Initializes the learner, loading existing patterns from Qdrant.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_isInitialized) return;

        await _syncLock.WaitAsync(ct);
        try
        {
            if (_isInitialized) return;

            // Detect vector size from embedding model
            await DetectVectorSizeAsync(ct);

            // Ensure collection exists with correct dimensions
            await EnsureCollectionExistsAsync(ct);

            // Load existing patterns into cache
            await LoadPatternsFromQdrantAsync(ct);

            // Add tool knowledge to MeTTa
            await InitializeMeTTaKnowledgeAsync(ct);

            _isInitialized = true;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    /// <summary>
    /// Finds or creates the best tool for a given goal using intelligent matching.
    /// Uses MeTTa for semantic understanding, checks Qdrant for existing patterns,
    /// and optionally evolves new configurations using genetic algorithms.
    /// </summary>
    /// <param name="goal">The user's goal (e.g., "search for Python tutorials").</param>
    /// <param name="registry">The tool registry to check for existing tools.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The best matching or newly created tool.</returns>
    public async Task<Result<(ITool Tool, bool WasCreated), string>> FindOrCreateToolAsync(
        string goal,
        ToolRegistry registry,
        CancellationToken ct = default)
    {
        if (!_isInitialized)
            await InitializeAsync(ct);

        try
        {
            // Step 1: Check Qdrant for existing learned patterns
            var existingPattern = await FindMatchingPatternAsync(goal, ct);
            if (existingPattern != null)
            {
                // Secondary check: verify the tool name is semantically related to the goal
                // This prevents generic embeddings from matching unrelated tools
                if (!IsToolRelevantToGoal(existingPattern.ToolName, goal))
                {
                    // Tool name doesn't seem related to goal - skip this pattern
                    existingPattern = null;
                }
            }

            if (existingPattern != null)
            {
                // Found a pattern - retrieve or recreate the tool
                var existingTool = registry.Get(existingPattern.ToolName);
                if (existingTool != null)
                {
                    await RecordPatternUsageAsync(existingPattern.Id, true, ct);
                    return Result<(ITool, bool), string>.Success((existingTool, false));
                }

                // Tool doesn't exist but we have the config - recreate it
                var recreateResult = await RecreateToolFromPatternAsync(existingPattern, registry, ct);
                if (recreateResult.IsSuccess)
                {
                    return Result<(ITool, bool), string>.Success((recreateResult.Value, false));
                }
            }

            // Step 2: Use MeTTa to reason about what tool is needed
            var toolAnalysis = await AnalyzeGoalWithMeTTaAsync(goal, ct);

            // Step 3: Check if we already have a suitable tool
            var recommendedTools = await _mettaRepresentation.QueryToolsForGoalAsync(goal, ct);
            if (recommendedTools.IsSuccess && recommendedTools.Value.Count > 0)
            {
                foreach (var recToolName in recommendedTools.Value)
                {
                    // Verify tool name is relevant before accepting
                    if (!IsToolRelevantToGoal(recToolName, goal))
                        continue;

                    var existingTool = registry.Get(recToolName);
                    if (existingTool != null)
                    {
                        return Result<(ITool, bool), string>.Success((existingTool, false));
                    }
                }
            }

            // Step 4: Create a new tool with optimized configuration
            var (inferredToolName, description) = await InferToolSpecFromGoalAsync(goal, toolAnalysis, ct);

            // Step 5: Evolve optimal configuration using genetic algorithm
            var optimalConfig = await EvolveOptimalConfigurationAsync(inferredToolName, description, goal, ct);

            // Step 6: Create the tool
            var createResult = await _toolFactory.CreateToolAsync(inferredToolName, description, ct);
            if (!createResult.IsSuccess)
            {
                return Result<(ITool, bool), string>.Failure($"Failed to create tool: {createResult.Error}");
            }

            var newTool = createResult.Value;
            // Note: ToolRegistry is immutable, caller is responsible for updating registry

            // Step 7: Persist the learned pattern to Qdrant
            await PersistPatternAsync(goal, inferredToolName, optimalConfig, ct);

            // Step 8: Add to MeTTa knowledge base
            await _mettaRepresentation.TranslateToolsAsync(registry.WithTool(newTool), ct);

            return Result<(ITool, bool), string>.Success((newTool, true));
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            return Result<(ITool, bool), string>.Failure($"Intelligent tool creation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Uses genetic algorithm to evolve optimal tool configuration.
    /// </summary>
    private async Task<ToolConfiguration> EvolveOptimalConfigurationAsync(
        string toolName,
        string description,
        string goal,
        CancellationToken ct = default)
    {
        try
        {
            // Create initial population of configurations
            var initialPopulation = CreateInitialConfigurationPopulation(toolName, description, 10);

            // Define fitness function based on goal alignment
            var fitnessFunction = new ToolConfigurationFitness(_llm, goal);

            // Create genetic algorithm
            var ga = new GeneticAlgorithm<ToolConfigurationGene>(
                fitnessFunction,
                MutateGene,
                mutationRate: 0.15,
                crossoverRate: 0.7,
                elitismRate: 0.2);

            // Evolve over a few generations (keep it fast for interactive use)
            var result = await ga.EvolveAsync(initialPopulation, generations: 5, ct);

            if (result.IsSuccess)
            {
                var bestChromosome = result.Value as ToolConfigurationChromosome;
                return bestChromosome?.ToConfiguration() ?? ToolConfiguration.Default(toolName, description);
            }

            // Fall back to default configuration
            return ToolConfiguration.Default(toolName, description);
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException)
        {
            // Fall back to default configuration on any error
            return ToolConfiguration.Default(toolName, description);
        }
    }

    /// <summary>
    /// Creates initial population of tool configurations for genetic algorithm.
    /// </summary>
    private List<IChromosome<ToolConfigurationGene>> CreateInitialConfigurationPopulation(
        string toolName,
        string description,
        int size)
    {
        var random = new Random();
        var population = new List<IChromosome<ToolConfigurationGene>>();

        string[] providers = { "duckduckgo", "google", "bing", null! };
        double[] timeouts = { 10.0, 20.0, 30.0, 45.0, 60.0 };
        int[] retries = { 1, 2, 3, 5 };

        for (int i = 0; i < size; i++)
        {
            var genes = new List<ToolConfigurationGene>
            {
                new("name", toolName),
                new("description", description),
                new("searchProvider", providers[random.Next(providers.Length)]),
                new("timeout", timeouts[random.Next(timeouts.Length)].ToString()),
                new("retries", retries[random.Next(retries.Length)].ToString()),
                new("cacheResults", random.NextDouble() > 0.3 ? "true" : "false"),
                new("customParams", null)
            };

            population.Add(new ToolConfigurationChromosome(genes));
        }

        return population;
    }

    /// <summary>
    /// Mutation function for genetic algorithm.
    /// </summary>
    private static ToolConfigurationGene MutateGene(ToolConfigurationGene gene)
    {
        var random = new Random();

        return gene.Key switch
        {
            "timeout" => new ToolConfigurationGene(gene.Key,
                (double.Parse(gene.Value ?? "30") * (0.8 + random.NextDouble() * 0.4)).ToString("F1")),
            "retries" => new ToolConfigurationGene(gene.Key,
                Math.Max(1, int.Parse(gene.Value ?? "3") + random.Next(-1, 2)).ToString()),
            "cacheResults" => new ToolConfigurationGene(gene.Key,
                random.NextDouble() > 0.5 ? "true" : "false"),
            "searchProvider" => new ToolConfigurationGene(gene.Key,
                new[] { "duckduckgo", "google", "bing" }[random.Next(3)]),
            _ => gene
        };
    }

    /// <summary>
    /// Gets statistics about learned patterns.
    /// </summary>
    public (int TotalPatterns, double AvgSuccessRate, int TotalUsage) GetStats()
    {
        var patterns = _patternCache.Values.ToList();
        if (patterns.Count == 0) return (0, 0, 0);

        return (
            patterns.Count,
            patterns.Average(p => p.SuccessRate),
            patterns.Sum(p => p.UsageCount)
        );
    }

    /// <summary>
    /// Gets all learned patterns.
    /// </summary>
    public IReadOnlyList<LearnedToolPattern> GetAllPatterns() =>
        _patternCache.Values.OrderByDescending(p => p.SuccessRate).ToList();

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _syncLock.Dispose();
        await ValueTask.CompletedTask;
    }
}

#region Genetic Algorithm Support Types

#endregion
