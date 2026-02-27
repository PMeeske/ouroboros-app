// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.

using Ouroboros.Application.Services;
using Ouroboros.Domain.Autonomous;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Provides shared state for autonomous tools via dependency injection,
/// replacing the previous static singleton properties on <see cref="AutonomousTools"/>.
/// </summary>
public interface IAutonomousToolContext
{
    // ── Core coordinator ──────────────────────────────────────────────────
    /// <summary>Shared autonomous coordinator reference.</summary>
    AutonomousCoordinator? Coordinator { get; set; }

    // ── MeTTa orchestration ───────────────────────────────────────────────
    /// <summary>Shared parallel MeTTa thought-streams orchestrator.</summary>
    ParallelMeTTaThoughtStreams? MeTTaOrchestrator { get; set; }

    // ── Pipeline state ────────────────────────────────────────────────────
    /// <summary>Shared CLI pipeline state for DSL tool continuity.</summary>
    CliPipelineState? PipelineState { get; set; }

    // ── LLM delegate functions ────────────────────────────────────────────
    /// <summary>Delegate for Ollama / chat-model inference.</summary>
    Func<string, CancellationToken, Task<string>>? OllamaFunction { get; set; }

    /// <summary>Delegate for web search.</summary>
    Func<string, CancellationToken, Task<string>>? SearchFunction { get; set; }

    /// <summary>Delegate for LLM evaluation (verify-claim).</summary>
    Func<string, CancellationToken, Task<string>>? EvaluateFunction { get; set; }

    /// <summary>Delegate for LLM reasoning (reasoning-chain).</summary>
    Func<string, CancellationToken, Task<string>>? ReasonFunction { get; set; }

    /// <summary>Delegate for executing a named tool (parallel-tools).</summary>
    Func<string, string, CancellationToken, Task<string>>? ExecuteToolFunction { get; set; }

    /// <summary>Delegate for LLM summarization (compress-context).</summary>
    Func<string, CancellationToken, Task<string>>? SummarizeFunction { get; set; }

    /// <summary>Delegate for LLM critique (self-doubt).</summary>
    Func<string, CancellationToken, Task<string>>? CritiqueFunction { get; set; }

    // ── Episodic memory bridges ───────────────────────────────────────────
    /// <summary>Optional delegate to persist an episodic memory to an external store.</summary>
    Func<string, string, double, CancellationToken, Task>? EpisodicExternalStoreFunc { get; set; }

    /// <summary>Optional delegate to recall episodic memories from an external store.</summary>
    Func<string, int, CancellationToken, Task<IEnumerable<string>>>? EpisodicExternalRecallFunc { get; set; }

    // ── Cognitive stream ──────────────────────────────────────────────────
    /// <summary>Optional callback to emit Ouroboros atom events to the cognitive stream.</summary>
    Action<string>? CognitiveEmitFunc { get; set; }
}
