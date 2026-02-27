// <copyright file="ApproveChangeTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Domain.SelfModification;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Approves a change proposal.
/// </summary>
public static partial class GitReflectionTools
{
    /// <summary>
    /// Approves a change proposal.
    /// </summary>
    public class ApproveChangeTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "approve_code_change";

        /// <inheritdoc/>
        public string Description => "Approve a pending code change proposal. Input JSON: {\"id\": \"proposal_id\", \"comment\": \"optional review comment\"}";

        /// <inheritdoc/>
        public string? JsonSchema => """{"type":"object","properties":{"id":{"type":"string"},"comment":{"type":"string"}},"required":["id"]}""";

        /// <inheritdoc/>
        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                JsonElement args = JsonSerializer.Deserialize<JsonElement>(input);
                string id = args.GetProperty("id").GetString() ?? "";
                string? comment = args.TryGetProperty("comment", out JsonElement commentProp) ? commentProp.GetString() : null;

                GitReflectionService service = GetService();
                bool success = service.ApproveProposal(id, comment);

                return success
                    ? Result<string, string>.Success($"\u2705 Proposal `{id}` approved. Use `apply_code_change` to apply it.")
                    : Result<string, string>.Failure($"Proposal `{id}` not found");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Approval failed: {ex.Message}");
            }
        }
    }
}
