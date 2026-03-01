// <copyright file="ImmersivePersona.Symbolic.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>


namespace Ouroboros.Application.Personality;

using Ouroboros.Core.Hyperon;
using Ouroboros.Tools.MeTTa;

/// <summary>
/// Hyperon symbolic reasoning API for ImmersivePersona.
/// </summary>
public sealed partial class ImmersivePersona
{
    /// <summary>
    /// Queries the Hyperon AtomSpace with a MeTTa pattern.
    /// </summary>
    /// <param name="pattern">MeTTa query pattern (e.g., "(match &amp;self (Thought $x $type) $x)")</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Query results as string representation</returns>
    public async Task<string> QuerySymbolicAsync(string pattern, CancellationToken ct = default)
    {
        if (_hyperonFlow == null)
            return "Hyperon not initialized";

        Result<string, string> result = await _hyperonFlow.Engine.ExecuteQueryAsync(pattern, ct);
        return result.IsSuccess ? result.Value : $"Error: {result.Error}";
    }

    /// <summary>
    /// Adds a belief to the persona's symbolic knowledge base.
    /// </summary>
    /// <param name="belief">MeTTa fact (e.g., "(believes Astra (important learning))")</param>
    /// <param name="ct">Cancellation token</param>
    public async Task AddBeliefAsync(string belief, CancellationToken ct = default)
    {
        if (_hyperonFlow == null) return;

        await _hyperonFlow.Engine.AddFactAsync(belief, ct);
    }

    /// <summary>
    /// Adds an intention that may trigger planning and action.
    /// </summary>
    /// <param name="goal">The goal to intend</param>
    /// <param name="priority">Priority level (0.0-1.0)</param>
    /// <param name="ct">Cancellation token</param>
    public async Task AddIntentionAsync(string goal, double priority = 0.5, CancellationToken ct = default)
    {
        if (_hyperonFlow == null) return;

        var intentionAtom = Atom.Expr(
            Atom.Sym("Intention"),
            Atom.Sym($"\"{goal}\""),
            Atom.Sym(priority.ToString("F2")));

        _hyperonFlow.Engine.AddAtom(intentionAtom);

        // Trigger intention resolution flow
        await _hyperonFlow.ExecuteFlowAsync("intention-resolution", ct);
    }

    /// <summary>
    /// Creates a new reasoning flow for the persona.
    /// </summary>
    /// <param name="name">Flow name</param>
    /// <param name="description">Flow description</param>
    /// <returns>A chainable HyperonFlow builder</returns>
    public HyperonFlow? CreateReasoningFlow(string name, string description)
    {
        return _hyperonFlow?.CreateFlow(name, description);
    }

    /// <summary>
    /// Executes a named reasoning flow.
    /// </summary>
    /// <param name="flowName">Name of the flow to execute</param>
    /// <param name="ct">Cancellation token</param>
    public async Task ExecuteReasoningFlowAsync(string flowName, CancellationToken ct = default)
    {
        if (_hyperonFlow == null) return;
        await _hyperonFlow.ExecuteFlowAsync(flowName, ct);
    }

    /// <summary>
    /// Subscribes to a symbolic pattern match in the AtomSpace.
    /// </summary>
    /// <param name="subscriptionId">Unique subscription identifier</param>
    /// <param name="pattern">MeTTa pattern to match</param>
    /// <param name="handler">Handler invoked on match</param>
    public void SubscribeToSymbolicPattern(
        string subscriptionId,
        string pattern,
        Action<PatternMatch> handler)
    {
        _hyperonFlow?.SubscribePattern(subscriptionId, pattern, handler);
    }

    /// <summary>
    /// Exports the current AtomSpace state to MeTTa source.
    /// </summary>
    /// <returns>MeTTa source code representation</returns>
    public string ExportKnowledgeBase()
    {
        if (_hyperonFlow == null)
            return "; Hyperon not initialized";

        return _hyperonFlow.Engine.ExportToMeTTa();
    }

    /// <summary>
    /// Loads MeTTa source into the persona's knowledge base.
    /// </summary>
    /// <param name="mettaSource">MeTTa source code</param>
    /// <param name="ct">Cancellation token</param>
    public async Task LoadKnowledgeAsync(string mettaSource, CancellationToken ct = default)
    {
        if (_hyperonFlow == null) return;

        await _hyperonFlow.Engine.LoadMeTTaSourceAsync(mettaSource, ct);
    }

    /// <summary>
    /// Gets all current intentions in the AtomSpace.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of intention descriptions</returns>
    public async Task<List<string>> GetActiveIntentionsAsync(CancellationToken ct = default)
    {
        if (_hyperonFlow == null)
            return new List<string>();

        Result<string, string> result = await _hyperonFlow.Engine.ExecuteQueryAsync(
            "(match &self (Intention $goal $priority) (: $goal $priority))",
            ct);

        // Parse results into list
        var intentions = new List<string>();
        if (result.IsSuccess && !string.IsNullOrEmpty(result.Value) && !result.Value.Contains("Empty"))
        {
            intentions.Add(result.Value);
        }
        return intentions;
    }

    /// <summary>
    /// Triggers meta-cognition - thinking about thinking.
    /// </summary>
    /// <param name="depth">Recursion depth for reflection</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Meta-cognitive insights</returns>
    public async Task<List<string>> ReflectAsync(int depth = 2, CancellationToken ct = default)
    {
        var insights = new List<string>();
        if (_hyperonFlow == null) return insights;

        var engine = _hyperonFlow.Engine;

        // Query self-knowledge
        Result<string, string> selfKnowledge = await engine.ExecuteQueryAsync(
            $"(match &self (is-a {Identity.Name} $type) $type)",
            ct);
        if (selfKnowledge.IsSuccess && !string.IsNullOrEmpty(selfKnowledge.Value))
        {
            insights.Add($"Self-identity: {selfKnowledge.Value}");
        }

        // Query beliefs about self
        Result<string, string> beliefs = await engine.ExecuteQueryAsync(
            $"(match &self (believes {Identity.Name} $belief) $belief)",
            ct);
        if (beliefs.IsSuccess && !string.IsNullOrEmpty(beliefs.Value) && !beliefs.Value.Contains("Empty"))
        {
            insights.Add($"Beliefs: {beliefs.Value}");
        }

        // Query emotional patterns
        Result<string, string> emotions = await engine.ExecuteQueryAsync(
            "(match &self (Emotion $name $intensity) (: $name $intensity))",
            ct);
        if (emotions.IsSuccess && !string.IsNullOrEmpty(emotions.Value) && !emotions.Value.Contains("Empty"))
        {
            insights.Add($"Emotional state: {emotions.Value}");
        }

        // Recursive reflection
        if (depth > 1)
        {
            Result<string, string> metaReflection = await engine.ExecuteQueryAsync(
                "(match &self (meta-cognition $self $thought $time) $thought)",
                ct);
            if (metaReflection.IsSuccess && !string.IsNullOrEmpty(metaReflection.Value) && !metaReflection.Value.Contains("Empty"))
            {
                insights.Add($"Meta-cognition (depth {depth}): thinking about {metaReflection.Value}");
            }
        }

        return insights;
    }
}
