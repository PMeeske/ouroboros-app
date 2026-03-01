// <copyright file="CompressContextTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Context compression tool that summarizes long contexts.
/// Addresses context window limitations.
/// </summary>
public class CompressContextTool : ITool
{
    private readonly IAutonomousToolContext _ctx;
    public CompressContextTool(IAutonomousToolContext context) => _ctx = context;
    public CompressContextTool() : this(AutonomousTools.DefaultContext) { }

    public string Name => "compress_context";
    public string Description => "Compress long context into essential summary. Overcomes context window limits. Input: JSON {\"content\":\"...\", \"target_tokens\":500, \"preserve\":[\"keywords\"]}";
    public string? JsonSchema => null;

    /// <summary>
    /// Delegate for LLM summarization. Delegates to <see cref="IAutonomousToolContext.SummarizeFunction"/>.
    /// </summary>
    public static Func<string, CancellationToken, Task<string>>? SummarizeFunction
    {
        get => AutonomousTools.DefaultContext.SummarizeFunction;
        set => AutonomousTools.DefaultContext.SummarizeFunction = value;
    }

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(input);
            var content = doc.RootElement.GetProperty("content").GetString() ?? "";
            var targetTokens = doc.RootElement.TryGetProperty("target_tokens", out var tt) ? tt.GetInt32() : 500;
            var preserve = new List<string>();
            if (doc.RootElement.TryGetProperty("preserve", out var pa))
            {
                foreach (var p in pa.EnumerateArray())
                {
                    var kw = p.GetString();
                    if (!string.IsNullOrEmpty(kw)) preserve.Add(kw);
                }
            }

            if (string.IsNullOrWhiteSpace(content))
                return Result<string, string>.Failure("Content is required.");

            // Estimate current tokens (rough: 4 chars per token)
            var currentTokens = content.Length / 4;

            if (currentTokens <= targetTokens)
                return Result<string, string>.Success($"\ud83d\udce6 Content already within target ({currentTokens} \u2264 {targetTokens} tokens).\n\n{content}");

            if (_ctx.SummarizeFunction == null)
            {
                // Fallback: simple truncation with sentence preservation
                var sentences = content.Split(new[] { ". ", "! ", "? " }, StringSplitOptions.RemoveEmptyEntries);
                var compressed = new StringBuilder();
                var charLimit = targetTokens * 4;

                foreach (var sentence in sentences)
                {
                    if (compressed.Length + sentence.Length > charLimit) break;

                    // Prioritize sentences with preserved keywords
                    var hasKeyword = preserve.Count == 0 || preserve.Any(kw => sentence.Contains(kw, StringComparison.OrdinalIgnoreCase));
                    if (hasKeyword || compressed.Length < charLimit / 2)
                    {
                        compressed.Append(sentence.Trim()).Append(". ");
                    }
                }

                return Result<string, string>.Success($"\ud83d\udce6 **Compressed** ({currentTokens} \u2192 ~{compressed.Length / 4} tokens)\n\n{compressed}");
            }

            // Use LLM for intelligent summarization
            var preserveInstructions = preserve.Count > 0
                ? $"\n\nIMPORTANT: Preserve information about: {string.Join(", ", preserve)}"
                : "";

            var prompt = $"Compress this content to approximately {targetTokens} tokens while preserving key information and meaning.{preserveInstructions}\n\nCONTENT:\n{content}";

            var compressed2 = await _ctx.SummarizeFunction(prompt, ct);

            return Result<string, string>.Success($"\ud83d\udce6 **Compressed** ({currentTokens} \u2192 ~{compressed2.Length / 4} tokens)\n\n{compressed2}");
        }
        catch (JsonException ex)
        {
            return Result<string, string>.Failure($"Compression failed: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"Compression failed: {ex.Message}");
        }
    }
}
