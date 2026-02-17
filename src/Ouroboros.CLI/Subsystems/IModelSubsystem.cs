using Ouroboros.Abstractions.Core;

namespace Ouroboros.CLI.Subsystems;

/// <summary>
/// Manages LLM models, embeddings, multi-model orchestration, and cost tracking.
/// </summary>
public interface IModelSubsystem : IAgentSubsystem
{
    IChatCompletionModel? ChatModel { get; }
    ToolAwareChatModel? Llm { get; set; }
    IEmbeddingModel? Embedding { get; }
    OrchestratedChatModel? OrchestratedModel { get; }
    DivideAndConquerOrchestrator? DivideAndConquer { get; }
    IChatCompletionModel? CoderModel { get; }
    IChatCompletionModel? ReasonModel { get; }
    IChatCompletionModel? SummarizeModel { get; }
    IChatCompletionModel? VisionChatModel { get; }
    Ouroboros.Core.EmbodiedInteraction.IVisionModel? VisionModel { get; }
    LlmCostTracker? CostTracker { get; }

    /// <summary>
    /// Returns the orchestrated model if available, otherwise the base chat model.
    /// </summary>
    IChatCompletionModel? GetEffectiveModel();

    /// <summary>
    /// Generates text using orchestration if available, falling back to single model.
    /// </summary>
    Task<string> GenerateWithOrchestrationAsync(string prompt, CancellationToken ct = default);
}