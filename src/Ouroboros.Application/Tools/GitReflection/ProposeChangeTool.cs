// <copyright file="ProposeChangeTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Ouroboros.Domain.SelfModification;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Proposes a code change.
/// </summary>
public static partial class GitReflectionTools
{
    /// <summary>
    /// Proposes a code change.
    /// </summary>
    public class ProposeChangeTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "propose_code_change";

        /// <inheritdoc/>
        public string Description => """
            Propose a change to my own source code for review before applying.
            Input JSON: {
                "file": "relative/path/to/file.cs",
                "description": "what the change does",
                "rationale": "why this change is needed",
                "old_code": "exact code to replace",
                "new_code": "replacement code",
                "category": "BugFix|Performance|Refactoring|Feature|Documentation|Testing|Security"
            }
            Returns a proposal ID that can be approved and applied.
            """;

        /// <inheritdoc/>
        public string? JsonSchema => """{"type":"object","properties":{"file":{"type":"string"},"description":{"type":"string"},"rationale":{"type":"string"},"old_code":{"type":"string"},"new_code":{"type":"string"},"category":{"type":"string"}},"required":["file","description","rationale","old_code","new_code","category"]}""";

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
                string categoryStr = args.GetProperty("category").GetString() ?? "Refactoring";

                if (!Enum.TryParse<ChangeCategory>(categoryStr, true, out ChangeCategory category))
                {
                    category = ChangeCategory.Refactoring;
                }

                GitReflectionService service = GetService();
                CodeChangeProposal proposal = service.ProposeChange(file, description, rationale, oldCode, newCode, category, RiskLevel.Medium);

                StringBuilder sb = new();
                sb.AppendLine($"\ud83d\udcdd **Change Proposal Created**");
                sb.AppendLine($"**ID:** `{proposal.Id}`");
                sb.AppendLine($"**File:** {proposal.FilePath}");
                sb.AppendLine($"**Category:** {proposal.Category}");
                sb.AppendLine($"**Risk:** {proposal.Risk}");
                sb.AppendLine($"**Description:** {proposal.Description}");
                sb.AppendLine($"\nTo apply: use `approve_code_change` with ID `{proposal.Id}`, then `apply_code_change`");

                return Result<string, string>.Success(sb.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to create proposal: {ex.Message}");
            }
        }
    }
}
