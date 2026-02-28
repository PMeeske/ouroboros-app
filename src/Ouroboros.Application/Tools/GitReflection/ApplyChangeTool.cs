// <copyright file="ApplyChangeTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Domain.SelfModification;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Applies an approved change.
/// </summary>
public static partial class GitReflectionTools
{
    /// <summary>
    /// Applies an approved change.
    /// </summary>
    public class ApplyChangeTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "apply_code_change";

        /// <inheritdoc/>
        public string Description => "Apply an approved code change proposal. Input JSON: {\"id\": \"proposal_id\", \"auto_commit\": true}";

        /// <inheritdoc/>
        public string? JsonSchema => """{"type":"object","properties":{"id":{"type":"string"},"auto_commit":{"type":"boolean"}},"required":["id"]}""";

        /// <inheritdoc/>
        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                JsonElement args = JsonSerializer.Deserialize<JsonElement>(input);
                string id = args.GetProperty("id").GetString() ?? "";
                bool autoCommit = args.TryGetProperty("auto_commit", out JsonElement commitProp) && commitProp.GetBoolean();

                GitReflectionService service = GetService();
                GitOperationResult result = await service.ApplyProposalAsync(id, autoCommit, ct);

                return result.Success
                    ? Result<string, string>.Success($"\u2705 {result.Message}\n\n\u26A0\uFE0F Note: Run `dotnet build` to verify changes compile correctly.")
                    : Result<string, string>.Failure(result.Message);
            }
            catch (JsonException ex)
            {
                return Result<string, string>.Failure($"Apply failed: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                return Result<string, string>.Failure($"Apply failed: {ex.Message}");
            }
        }
    }
}
