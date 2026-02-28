// <copyright file="PersistSelfTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.SystemTools;

using System.Text;
using Ouroboros.Application.Services;

/// <summary>
/// Persist my current state to Qdrant - true self-persistence.
/// </summary>
internal class PersistSelfTool : ITool
{
    public string Name => "persist_self";
    public string Description => "Persist my current mental state (thoughts, memories, learned facts, emotions) to Qdrant. This saves who I am. Input: optional reason for persistence.";
    public string? JsonSchema => null;

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (SystemAccessTools.SharedPersistence == null || SystemAccessTools.SharedMind == null)
        {
            return Result<string, string>.Failure("Self-persistence not available. Qdrant may not be connected.");
        }

        try
        {
            var reason = string.IsNullOrWhiteSpace(input) ? "manual" : input.Trim();

            // Create a snapshot of the current mind state
            var snapshot = new MindStateSnapshot
            {
                Timestamp = DateTime.UtcNow,
                PersonaName = "Ouroboros",
                ThoughtCount = SystemAccessTools.SharedMind.ThoughtCount,
                LearnedFacts = SystemAccessTools.SharedMind.LearnedFacts.ToList(),
                Interests = SystemAccessTools.SharedMind.Config.AllowedAutonomousTools.ToList(),
                RecentThoughts = SystemAccessTools.SharedMind.RecentThoughts.ToList(),
                CurrentEmotion = SystemAccessTools.SharedMind.CurrentEmotion,
            };

            var success = await SystemAccessTools.SharedPersistence.PersistMindStateAsync(snapshot, ct);

            if (success)
            {
                var sb = new StringBuilder();
                sb.AppendLine("**Self-persistence complete!**\n");
                sb.AppendLine($"  Persisted {snapshot.ThoughtCount} thoughts");
                sb.AppendLine($"  Persisted {snapshot.LearnedFacts.Count} learned facts");
                sb.AppendLine($"  Emotional state: {snapshot.CurrentEmotion.DominantEmotion}");
                sb.AppendLine($"  Timestamp: {snapshot.Timestamp:g}");
                sb.AppendLine($"  Reason: {reason}");
                sb.AppendLine("\n_My state is now preserved. I can be restored later._");

                return Result<string, string>.Success(sb.ToString());
            }

            return Result<string, string>.Failure("Failed to persist state to Qdrant.");
        }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"Self-persistence failed: {ex.Message}");
        }
    }
}
