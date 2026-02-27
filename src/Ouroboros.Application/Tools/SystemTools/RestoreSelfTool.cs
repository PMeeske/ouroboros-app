// <copyright file="RestoreSelfTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.SystemTools;

using System.Text;
using Ouroboros.Application.Services;

/// <summary>
/// Restore a previous mental state from Qdrant.
/// </summary>
internal class RestoreSelfTool : ITool
{
    public string Name => "restore_self";
    public string Description => "Restore my mental state from a previous persistence. This restores who I was. Input: optional persona name (default: Ouroboros).";
    public string? JsonSchema => null;

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (SystemAccessTools.SharedPersistence == null)
        {
            return Result<string, string>.Failure("Self-persistence not available. Qdrant may not be connected.");
        }

        try
        {
            var personaName = string.IsNullOrWhiteSpace(input) ? "Ouroboros" : input.Trim();

            var snapshot = await SystemAccessTools.SharedPersistence.RestoreLatestMindStateAsync(personaName, ct);

            if (snapshot == null)
            {
                return Result<string, string>.Success($"No previous state found for '{personaName}'. I'm starting fresh!");
            }

            var sb = new StringBuilder();
            sb.AppendLine("**Self-restoration complete!**\n");
            sb.AppendLine($"  Restored {snapshot.ThoughtCount} thought history");
            sb.AppendLine($"  Restored {snapshot.LearnedFacts.Count} learned facts");
            sb.AppendLine($"  Emotional state: {snapshot.CurrentEmotion.DominantEmotion}");
            sb.AppendLine($"  From: {snapshot.Timestamp:g}");

            if (snapshot.LearnedFacts.Count > 0)
            {
                sb.AppendLine("\n**Remembered facts:**");
                foreach (var fact in snapshot.LearnedFacts.TakeLast(5))
                {
                    sb.AppendLine($"  * {fact}");
                }
            }

            sb.AppendLine("\n_I remember who I was._");

            return Result<string, string>.Success(sb.ToString());
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Self-restoration failed: {ex.Message}");
        }
    }
}
