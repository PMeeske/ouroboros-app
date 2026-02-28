// <copyright file="PersistenceStatsTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.SystemTools;

using System.Text;
using Ouroboros.Application.Services;

/// <summary>
/// Get statistics about self-persistence.
/// </summary>
internal class PersistenceStatsTool : ITool
{
    public string Name => "persistence_stats";
    public string Description => "Get statistics about my self-persistence - how much I've saved about myself.";
    public string? JsonSchema => null;

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (SystemAccessTools.SharedPersistence == null)
        {
            return Result<string, string>.Failure("Self-persistence not available.");
        }

        try
        {
            var stats = await SystemAccessTools.SharedPersistence.GetStatsAsync(ct);

            var sb = new StringBuilder();
            sb.AppendLine("**Self-Persistence Statistics**\n");
            sb.AppendLine($"  Qdrant connected: {(stats.IsConnected ? "Yes" : "No")}");
            sb.AppendLine($"  Collection: {stats.CollectionName}");
            sb.AppendLine($"  Total persisted points: {stats.TotalPoints}");
            sb.AppendLine($"  File backups: {stats.FileBackups}");

            return Result<string, string>.Success(sb.ToString());
        }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"Failed to get stats: {ex.Message}");
        }
    }
}
