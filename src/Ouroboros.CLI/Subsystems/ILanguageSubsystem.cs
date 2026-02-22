// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using Ouroboros.CLI.Services;

/// <summary>
/// Detects the human language of spoken or typed text so ImmersiveMode and
/// RoomMode can instruct the LLM to respond in the user's language and update
/// the Azure TTS voice culture accordingly.
/// </summary>
public interface ILanguageSubsystem : IAgentSubsystem
{
    /// <summary>
    /// Detects the language of <paramref name="text"/> using an LLM,
    /// falling back to heuristic scoring on timeout or error.
    /// </summary>
    Task<LanguageDetector.DetectedLanguage> DetectAsync(
        string text, CancellationToken ct = default);
}
