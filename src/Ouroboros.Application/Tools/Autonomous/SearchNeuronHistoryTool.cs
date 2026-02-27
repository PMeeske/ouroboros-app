// <copyright file="SearchNeuronHistoryTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Searches neuron message history.
/// </summary>
public class SearchNeuronHistoryTool : ITool
{
    private readonly IAutonomousToolContext _ctx;
    public SearchNeuronHistoryTool(IAutonomousToolContext context) => _ctx = context;
    public SearchNeuronHistoryTool() : this(AutonomousTools.DefaultContext) { }

    /// <inheritdoc/>
    public string Name => "search_neuron_history";

    /// <inheritdoc/>
    public string Description => "Search my recent internal neural network message history. Input: search query.";

    /// <inheritdoc/>
    public string? JsonSchema => null;

    /// <inheritdoc/>
    public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (_ctx.Coordinator == null)
            return Task.FromResult(Result<string, string>.Failure("Autonomous coordinator not initialized."));

        var query = input.Trim().ToLowerInvariant();
        var messages = _ctx.Coordinator.Network.GetRecentMessages(100);

        var matches = messages
            .Where(m => m.Topic.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        m.Payload.ToString()?.Contains(query, StringComparison.OrdinalIgnoreCase) == true ||
                        m.SourceNeuron.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .ToList();

        if (matches.Count == 0)
            return Task.FromResult(Result<string, string>.Success($"No messages found matching: {query}"));

        var sb = new StringBuilder();
        sb.AppendLine($"\ud83d\udd0d Found {matches.Count} matching messages:\n");

        foreach (var msg in matches)
        {
            sb.AppendLine($"**{msg.Topic}** from `{msg.SourceNeuron}`");
            sb.AppendLine($"  {msg.Payload.ToString()?[..Math.Min(100, msg.Payload.ToString()?.Length ?? 0)]}...");
        }

        return Task.FromResult(Result<string, string>.Success(sb.ToString()));
    }
}
