// <copyright file="ImmersiveMode.RunAsync.Shutdown.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.CLI.Commands;

using Ouroboros.Application.Personality;
using Ouroboros.Application.Tools;
using Ouroboros.CLI.Infrastructure;
using Spectre.Console;

public sealed partial class ImmersiveMode
{
    /// <summary>
    /// Performs graceful shutdown: persists final network state, disposes room listener,
    /// avatar subsystem, OpenClaw client, and owned persona.
    /// </summary>
    private async Task ShutdownAsync(
        ImmersivePersona persona,
        ImmersivePersona? ownedPersona,
        Ouroboros.CLI.Services.RoomPresence.AmbientRoomListener? roomListener)
    {
        // Final consciousness state
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("\n  [~] Consciousness fading..."));
        PrintConsciousnessState(persona);

        // Persist final network state and learnings
        if (_networkStateProjector != null)
        {
            try
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Dim("  [~] Persisting learnings..."));
                // Use CancellationToken.None — session token is already cancelled at this point
                // (Ctrl+C fired), but we still want the final snapshot to complete.
                await _networkStateProjector.ProjectAndPersistAsync(
                    System.Collections.Immutable.ImmutableDictionary<string, string>.Empty
                        .Add("event", "session_end")
                        .Add("interactions", persona.InteractionCount.ToString())
                        .Add("uptime_minutes", persona.Uptime.TotalMinutes.ToString("F1")),
                    CancellationToken.None);
                AnsiConsole.MarkupLine(OuroborosTheme.Ok($"  [OK] State saved (epoch {_networkStateProjector.CurrentEpoch}, {_networkStateProjector.RecentLearnings.Count} learnings)"));
                await _networkStateProjector.DisposeAsync();
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Warn($"  [!] Failed to persist state: {ex.Message}"));
            }
        }

        // Dispose OpenClaw client
        if (OpenClawTools.SharedClient != null)
            await OpenClawTools.SharedClient.DisposeAsync();

        // Dispose room listener (if --room-mode was active)
        if (roomListener != null)
            await roomListener.DisposeAsync();

        // Dispose avatar subsystem
        if (_immersive != null)
            await _immersive.DisposeAsync();

        AnsiConsole.MarkupLine(Markup.Escape($"\n  Session complete. {persona.InteractionCount} interactions. Uptime: {persona.Uptime.TotalMinutes:F1} minutes."));

        // Dispose persona only if we own it (not provided by OuroborosAgent)
        if (ownedPersona != null)
            await ownedPersona.DisposeAsync();
    }
}
