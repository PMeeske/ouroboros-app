// <copyright file="VoiceModeService.Display.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Reactive.Linq;
using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Domain.Voice;
using Spectre.Console;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// Display and presence indicator methods for VoiceModeService:
/// SetupDisplayPipeline, SetupPresenceIndicators, PrintHeader.
/// </summary>
public sealed partial class VoiceModeService
{
    /// <summary>
    /// Prints voice mode header.
    /// </summary>
    public void PrintHeader(string commandName)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(OuroborosTheme.ThemedRule($"VOICE MODE - {commandName.ToUpperInvariant()} ({_persona.Name})"));
        var table = OuroborosTheme.ThemedTable("Property", "Value");
        table.AddRow(OuroborosTheme.Accent("Personality:"), Markup.Escape(_activeTraits));
        table.AddRow(OuroborosTheme.Accent("Mood:"), Markup.Escape(_currentMood));
        table.AddRow("", OuroborosTheme.Dim("Say 'help' for commands, 'goodbye' or 'exit' to quit"));
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Sets up the Rx display pipeline for colored console output.
    /// </summary>
    private void SetupDisplayPipeline()
    {
        // Display text output events with styling
        _disposables.Add(
            _stream.TextOutputs
                .Subscribe(e =>
                {
                    var escaped = Markup.Escape(e.Text);
                    var styled = e.Style switch
                    {
                        OutputStyle.Thinking => $"[grey]{escaped}[/]",
                        OutputStyle.Emphasis => $"[rgb(148,103,189)]{escaped}[/]",
                        OutputStyle.Whisper => $"[rgb(128,0,180)]{escaped}[/]",
                        OutputStyle.System => $"[yellow]{escaped}[/]",
                        OutputStyle.Error => $"[red]{escaped}[/]",
                        OutputStyle.UserInput => $"[green]{escaped}[/]",
                        _ => escaped,
                    };

                    if (e.Append)
                    {
                        AnsiConsole.Markup(styled);
                    }
                    else
                    {
                        AnsiConsole.MarkupLine(styled);
                    }
                }));

        // Display errors
        _disposables.Add(
            _stream.Errors
                .Subscribe(e =>
                {
                    var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
                    AnsiConsole.MarkupLine($"\n  [red]{Markup.Escape(face)} ✗ {Markup.Escape(e.Category.ToString())}: {Markup.Escape(e.Message)}[/]");
                }));
    }

    /// <summary>
    /// Sets up visual presence state indicators.
    /// </summary>
    private void SetupPresenceIndicators()
    {
        // Show visual state indicators [ ]/[*]/[...]/[>]
        _disposables.Add(
            _presence.State
                .DistinctUntilChanged()
                .Subscribe(state =>
                {
                    if (!_enableVisualIndicators || _config.VoiceOnly) return;

                    var indicator = state switch
                    {
                        AgentPresenceState.Idle => "[ ]",
                        AgentPresenceState.Listening => "[*]",
                        AgentPresenceState.Processing => "[...]",
                        AgentPresenceState.Speaking => "[>]",
                        AgentPresenceState.Interrupted => "[!]",
                        AgentPresenceState.Paused => "[-]",
                        _ => "[ ]",
                    };

                    AnsiConsole.Markup($"\r{Markup.Escape(indicator)} ");
                }));

        // Subscribe to barge-in events
        _presence.BargeInDetected += (_, e) =>
        {
            var snippet = e.UserInput?[..Math.Min(30, e.UserInput?.Length ?? 0)] ?? "";
            AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Warn("[Interrupted] " + snippet + "...")}");
        };
    }
}
