// <copyright file="OuroborosTheme.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Spectre.Console;
using Spectre.Console.Rendering;

namespace Ouroboros.CLI.Infrastructure;

/// <summary>
/// Purple/gold Ouroboros theme for Iaret's CLI presence.
/// Matches the violet-cosmic aesthetic of her avatar.
/// </summary>
public static class OuroborosTheme
{
    // ── Primary palette ──────────────────────────────────────────
    public static readonly Color Purple = new(128, 0, 180);
    public static readonly Color DeepPurple = new(60, 0, 120);
    public static readonly Color Violet = new(148, 103, 189);
    public static readonly Color Gold = new(255, 200, 50);
    public static readonly Color SoftGold = new(218, 175, 62);

    // ── Semantic colors ──────────────────────────────────────────
    public static readonly Color Success = Color.Green;
    public static readonly Color Warning = Color.Yellow;
    public static readonly Color Error = Color.Red1;
    public static readonly Color Muted = Color.Grey;

    // ── Named styles ─────────────────────────────────────────────
    public static readonly Style HeaderStyle = new(Gold, DeepPurple, Decoration.Bold);
    public static readonly Style BannerStyle = new(Color.White, DeepPurple);
    public static readonly Style BannerAccent = new(Gold, DeepPurple, Decoration.Bold);
    public static readonly Style AccentStyle = new(Violet);
    public static readonly Style GoldStyle = new(Gold);
    public static readonly Style MutedStyle = new(Muted);
    public static readonly Style BorderStyle = new(Violet);
    public static readonly Style PromptStyle = new(Gold);
    public static readonly Style SuccessStyle = new(Success);
    public static readonly Style ErrorStyle = new(Error);
    public static readonly Style WarningStyle = new(Warning);

    // ── Markup helpers ───────────────────────────────────────────

    /// <summary>
    /// Wraps text in purple accent markup.
    /// </summary>
    public static string Accent(string text) => $"[rgb(148,103,189)]{Markup.Escape(text)}[/]";

    /// <summary>
    /// Wraps text in gold markup.
    /// </summary>
    public static string GoldText(string text) => $"[rgb(255,200,50)]{Markup.Escape(text)}[/]";

    /// <summary>
    /// Wraps text in purple-background header markup.
    /// </summary>
    public static string Header(string text) => $"[bold rgb(255,200,50) on rgb(60,0,120)] {Markup.Escape(text)} [/]";

    /// <summary>
    /// Wraps text in success green markup.
    /// </summary>
    public static string Ok(string text) => $"[green]{Markup.Escape(text)}[/]";

    /// <summary>
    /// Wraps text in error red markup.
    /// </summary>
    public static string Err(string text) => $"[red]{Markup.Escape(text)}[/]";

    /// <summary>
    /// Wraps text in warning yellow markup.
    /// </summary>
    public static string Warn(string text) => $"[yellow]{Markup.Escape(text)}[/]";

    /// <summary>
    /// Wraps text in muted grey markup.
    /// </summary>
    public static string Dim(string text) => $"[grey]{Markup.Escape(text)}[/]";

    // ── Component builders ───────────────────────────────────────

    /// <summary>
    /// Creates a themed panel with violet border and optional purple-bg header.
    /// </summary>
    public static Panel ThemedPanel(IRenderable content, string? header = null)
    {
        var panel = new Panel(content)
        {
            Border = BoxBorder.Rounded,
            BorderStyle = BorderStyle,
            Padding = new Padding(1, 0, 1, 0),
        };
        if (header != null)
            panel.Header = new PanelHeader($"[bold rgb(255,200,50)] {Markup.Escape(header)} [/]", Justify.Center);
        return panel;
    }

    /// <summary>
    /// Creates a themed rule/divider with violet line and gold title.
    /// </summary>
    public static Spectre.Console.Rule ThemedRule(string? title = null)
    {
        var rule = title != null
            ? new Spectre.Console.Rule($"[bold rgb(255,200,50)]{Markup.Escape(title)}[/]")
            : new Spectre.Console.Rule();
        rule.Style = BorderStyle;
        return rule;
    }

    /// <summary>
    /// Creates a themed table with violet borders.
    /// </summary>
    public static Table ThemedTable(params string[] columns)
    {
        var table = new Table
        {
            Border = TableBorder.Rounded,
            BorderStyle = BorderStyle,
        };
        foreach (var col in columns)
            table.AddColumn(new TableColumn($"[bold rgb(148,103,189)]{Markup.Escape(col)}[/]"));
        return table;
    }
}
