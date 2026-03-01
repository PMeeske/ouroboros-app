// <copyright file="ListPendingIntentionsTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Lists pending intentions awaiting approval.
/// </summary>
public class ListPendingIntentionsTool : ITool
{
    private readonly IAutonomousToolContext _ctx;
    public ListPendingIntentionsTool(IAutonomousToolContext context) => _ctx = context;
    public ListPendingIntentionsTool() : this(AutonomousTools.DefaultContext) { }

    /// <inheritdoc/>
    public string Name => "list_my_intentions";

    /// <inheritdoc/>
    public string Description => "List my pending intentions that are waiting for user approval.";

    /// <inheritdoc/>
    public string? JsonSchema => null;

    /// <inheritdoc/>
    public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (_ctx.Coordinator == null)
            return Task.FromResult(Result<string, string>.Failure("Autonomous coordinator not initialized."));

        var pending = _ctx.Coordinator.IntentionBus.GetPendingIntentions();

        if (pending.Count == 0)
            return Task.FromResult(Result<string, string>.Success("\ud83d\udced No pending intentions."));

        var sb = new StringBuilder();
        sb.AppendLine($"\ud83d\udccb **{pending.Count} Pending Intention(s)**\n");

        foreach (var intention in pending)
        {
            var idShort = intention.Id.ToString()[..8];
            sb.AppendLine($"**{idShort}** | [{intention.Priority}] [{intention.Category}]");
            sb.AppendLine($"  \ud83d\udccc {intention.Title}");
            sb.AppendLine($"  \ud83d\udcdd {intention.Description}");
            sb.AppendLine($"  \ud83d\udca1 {intention.Rationale}");
            sb.AppendLine();
        }

        return Task.FromResult(Result<string, string>.Success(sb.ToString()));
    }
}
