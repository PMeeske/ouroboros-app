// <copyright file="SelfModifyTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Ouroboros.Domain.SelfModification;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Complete self-modification workflow.
/// </summary>
public static partial class GitReflectionTools
{
    /// <summary>
    /// Complete self-modification workflow.
    /// </summary>
    public class SelfModifyTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "self_modify";

        /// <inheritdoc/>
        public string Description => """
            Complete self-modification workflow: propose, approve (if low risk), and apply a code change.
            Input JSON: {
                "file": "relative/path/to/file.cs",
                "description": "what the change does",
                "rationale": "why this change improves me",
                "old_code": "exact code to replace",
                "new_code": "replacement code",
                "category": "BugFix|Performance|Refactoring|Feature|Documentation|Testing"
            }
            Low-risk changes are auto-approved. High-risk changes require manual approval.
            """;

        /// <inheritdoc/>
        public string? JsonSchema => """{"type":"object","properties":{"file":{"type":"string"},"description":{"type":"string"},"rationale":{"type":"string"},"old_code":{"type":"string"},"new_code":{"type":"string"},"category":{"type":"string"}},"required":["file","description","rationale","old_code","new_code"]}""";

        /// <inheritdoc/>
        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                JsonElement args = JsonSerializer.Deserialize<JsonElement>(input);
                string file = args.GetProperty("file").GetString() ?? "";
                string description = args.GetProperty("description").GetString() ?? "";
                string rationale = args.GetProperty("rationale").GetString() ?? "";
                string oldCode = args.GetProperty("old_code").GetString() ?? "";
                string newCode = args.GetProperty("new_code").GetString() ?? "";
                string categoryStr = args.TryGetProperty("category", out JsonElement catProp) ? catProp.GetString() ?? "Refactoring" : "Refactoring";

                if (!Enum.TryParse<ChangeCategory>(categoryStr, true, out ChangeCategory category))
                {
                    category = ChangeCategory.Refactoring;
                }

                GitReflectionService service = GetService();
                GitOperationResult result = await service.SelfModifyAsync(file, description, rationale, oldCode, newCode, category, autoApprove: true, ct);

                if (result.Success)
                {
                    StringBuilder sb = new();
                    sb.AppendLine("\ud83e\uddec **Self-Modification Complete**");
                    sb.AppendLine($"**File:** {file}");
                    sb.AppendLine($"**Change:** {description}");
                    sb.AppendLine($"**Branch:** {result.BranchName ?? "current"}");
                    sb.AppendLine($"\n\u26A0\uFE0F **Important:** Run `dotnet build` to verify the changes compile correctly.");
                    return Result<string, string>.Success(sb.ToString());
                }
                else
                {
                    return Result<string, string>.Failure(result.Message);
                }
            }
            catch (JsonException ex)
            {
                return Result<string, string>.Failure($"Self-modification failed: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                return Result<string, string>.Failure($"Self-modification failed: {ex.Message}");
            }
        }
    }
}
