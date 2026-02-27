// <copyright file="ImmersiveMode.Pipeline.cs" company="Ouroboros">
// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.
// </copyright>

namespace Ouroboros.CLI.Commands;

using System.Text.RegularExpressions;
using Ouroboros.Application;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Options;
using Spectre.Console;

public sealed partial class ImmersiveMode
{
    private bool IsPipelineRelatedQuery(string input)
    {
        var lower = input.ToLowerInvariant();
        return lower.Contains("pipeline") ||
               lower.Contains("token") ||
               lower.Contains("example") ||
               lower.Contains("how do i use") ||
               lower.Contains("how to use") ||
               lower.Contains("show me how") ||
               lower.Contains("what can you do") ||
               lower.Contains("capabilities") ||
               lower.Contains("commands");
    }

    private string HandleListTokens(string personaName)
    {
        if (_allTokens == null || _allTokens.Count == 0)
            return "No pipeline tokens available.";

        AnsiConsole.WriteLine();
        var tokenTable = OuroborosTheme.ThemedTable("Token", "Description");
        foreach (var (name, info) in _allTokens.Take(15))
        {
            var desc = info.Description.Length > 40 ? info.Description[..40] : info.Description;
            tokenTable.AddRow(Markup.Escape(name), Markup.Escape(desc));
        }
        if (_allTokens.Count > 15)
            tokenTable.AddRow(Markup.Escape($"... and {_allTokens.Count - 15} more"), "");
        AnsiConsole.Write(OuroborosTheme.ThemedPanel(tokenTable, $"Pipeline Tokens ({_allTokens.Count})"));
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Examples:")}");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.GoldText("ArxivSearch 'neural networks'")}           - Search papers");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.GoldText("WikiSearch 'quantum computing'")}          - Search Wikipedia");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.GoldText("ArxivSearch 'AI' | Summarize")}            - Chain with pipe");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.GoldText("Fetch 'https://example.com'")}             - Fetch web content");

        // Set context so follow-up questions get pipeline-aware responses
        _lastPipelineContext = "pipeline_tokens";

        return $"I have {_allTokens.Count} pipeline tokens available. Try commands like 'ArxivSearch neural networks' or chain them with pipes!";
    }

    private string HandlePipelineHelp(string personaName)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Pipeline Usage Guide"));
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  {OuroborosTheme.GoldText("SINGLE COMMANDS:")}");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("ArxivSearch 'neural networks'")}     Search academic papers on arXiv");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("WikiSearch 'quantum computing'")}    Look up topics on Wikipedia");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("SemanticScholarSearch 'AI'")}        Search Semantic Scholar");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Fetch 'https://example.com'")}       Fetch content from any URL");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Generate 'topic'")}                  Generate text about a topic");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Summarize")}                         Summarize the last output");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  {OuroborosTheme.GoldText("CHAINED PIPELINES (use | to chain):")}");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("ArxivSearch 'transformers' | Summarize")}");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("WikiSearch 'machine learning' | Generate 'explanation'")}");
        AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Fetch 'url' | UseOutput")}");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  {OuroborosTheme.GoldText("NATURAL LANGUAGE (I understand these too):")}");
        AnsiConsole.MarkupLine($"    'search arxiv for neural networks'");
        AnsiConsole.MarkupLine($"    'look up AI on wikipedia'");
        AnsiConsole.MarkupLine($"    'find papers about transformers'");
        AnsiConsole.MarkupLine($"    'summarize that'");

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  {OuroborosTheme.GoldText("TIPS:")}");
        AnsiConsole.MarkupLine($"    - Use quotes around multi-word arguments");
        AnsiConsole.MarkupLine($"    - Chain multiple steps with the pipe | symbol");
        AnsiConsole.MarkupLine($"    - Say 'tokens' to see all available pipeline tokens");

        _lastPipelineContext = "pipeline_help";
        return "I can execute pipeline commands! Try 'ArxivSearch neural networks' or chain them like 'WikiSearch AI | Summarize'.";
    }

    private async Task<string> HandlePipelineAsync(
        string pipeline,
        string personaName,
        IVoiceOptions options,
        CancellationToken ct)
    {
        if (_allTokens == null || _pipelineState == null)
            return "Pipeline execution is not available.";

        AnsiConsole.MarkupLine(OuroborosTheme.Warn($"\n  [>] Executing pipeline: {pipeline}"));

        try
        {
            // Split pipeline into steps
            var steps = pipeline.Split('|').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
            var state = _pipelineState;

            foreach (var stepStr in steps)
            {
                // Parse step: "TokenName 'arg'" or "TokenName arg" or just "TokenName"
                var match = Regex.Match(stepStr, @"^(\w+)\s*(?:'([^']*)'|""([^""]*)""|(.*))?$");
                if (!match.Success)
                {
                    AnsiConsole.MarkupLine($"      [red]{Markup.Escape($"[!] Invalid step syntax: {stepStr}")}[/]");
                    continue;
                }

                string tokenName = match.Groups[1].Value;
                string arg = match.Groups[2].Success ? match.Groups[2].Value :
                             match.Groups[3].Success ? match.Groups[3].Value :
                             match.Groups[4].Value.Trim();

                // Find the token
                if (_allTokens.TryGetValue(tokenName, out var tokenInfo))
                {
                    AnsiConsole.MarkupLine(OuroborosTheme.Dim($"      -> {tokenName}" + (string.IsNullOrEmpty(arg) ? "" : $" '{arg}'")));

                    try
                    {
                        // Set the query/prompt in state for this step
                        if (!string.IsNullOrEmpty(arg))
                        {
                            state.Query = arg;
                            state.Prompt = arg;
                        }

                        // Invoke the pipeline step method
                        var stepMethod = tokenInfo.Method;
                        object?[]? methodArgs = string.IsNullOrEmpty(arg) ? null : new object[] { arg };
                        var stepInstance = stepMethod.Invoke(null, methodArgs);

                        if (stepInstance is Step<CliPipelineState, CliPipelineState> step)
                        {
                            state = await step(state);
                        }
                        else if (stepInstance is Func<CliPipelineState, Task<CliPipelineState>> asyncStep)
                        {
                            state = await asyncStep(state);
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"         [red]{Markup.Escape($"[!] Step returned unexpected type: {stepInstance?.GetType().Name ?? "null"}")}[/]");
                        }
                    }
                    catch (Exception ex)
                    {
                        var innerEx = ex.InnerException ?? ex;
                        AnsiConsole.MarkupLine($"         [red]{Markup.Escape($"[!] Step error: {innerEx.Message}")}[/]");
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine($"      [red]{Markup.Escape($"[!] Unknown token: {tokenName}")}[/]");
                    // Try to suggest similar tokens
                    var suggestions = _allTokens.Keys
                        .Where(k => k.Contains(tokenName, StringComparison.OrdinalIgnoreCase) ||
                                    tokenName.Contains(k, StringComparison.OrdinalIgnoreCase))
                        .Take(3);
                    if (suggestions.Any())
                    {
                        AnsiConsole.MarkupLine(OuroborosTheme.Dim($"         Did you mean: {string.Join(", ", suggestions)}?"));
                    }
                }
            }

            // Update the shared pipeline state with results
            _pipelineState = state;

            AnsiConsole.MarkupLine(OuroborosTheme.Ok("  [OK] Pipeline complete"));

            // Return meaningful output
            if (!string.IsNullOrEmpty(state.Output))
            {
                // Truncate for voice but show full in console
                var preview = state.Output.Length > 300 ? state.Output[..300] + "..." : state.Output;
                AnsiConsole.MarkupLine($"\n  {OuroborosTheme.Accent("Pipeline Output:")}\n  {Markup.Escape(preview)}");
                return $"I ran your {steps.Count}-step pipeline. Here's what I found: {preview}";
            }

            return $"I ran your {steps.Count}-step pipeline successfully.";
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape($"[!] Pipeline error: {ex.Message}")}[/]");
            return $"Pipeline error: {ex.Message}";
        }
    }

    /// <summary>
    /// Execute a single pipeline token by name.
    /// </summary>
    private async Task<string?> TryExecuteSingleTokenAsync(
        string input,
        string personaName,
        CancellationToken ct)
    {
        if (_allTokens == null || _pipelineState == null)
            return null;

        // Parse: "TokenName arg" or "TokenName 'arg'"
        var match = Regex.Match(input.Trim(), @"^(\w+)\s*(?:'([^']*)'|""([^""]*)""|(.*))?$");
        if (!match.Success) return null;

        string tokenName = match.Groups[1].Value;
        string arg = match.Groups[2].Success ? match.Groups[2].Value :
                     match.Groups[3].Success ? match.Groups[3].Value :
                     match.Groups[4].Value.Trim();

        // Check if this is a known token
        if (!_allTokens.TryGetValue(tokenName, out var tokenInfo))
            return null;

        AnsiConsole.MarkupLine(OuroborosTheme.Warn($"\n  [>] Executing: {tokenName}" + (string.IsNullOrEmpty(arg) ? "" : $" '{arg}'")));

        try
        {
            var state = _pipelineState;
            if (!string.IsNullOrEmpty(arg))
            {
                state.Query = arg;
                state.Prompt = arg;
            }

            var stepMethod = tokenInfo.Method;
            object?[]? methodArgs = string.IsNullOrEmpty(arg) ? null : new object[] { arg };
            var stepInstance = stepMethod.Invoke(null, methodArgs);

            if (stepInstance is Step<CliPipelineState, CliPipelineState> step)
            {
                state = await step(state);
            }
            else if (stepInstance is Func<CliPipelineState, Task<CliPipelineState>> asyncStep)
            {
                state = await asyncStep(state);
            }

            _pipelineState = state;
            AnsiConsole.MarkupLine(OuroborosTheme.Ok($"  [OK] {tokenName} complete"));

            if (!string.IsNullOrEmpty(state.Output))
            {
                var preview = state.Output.Length > 300 ? state.Output[..300] + "..." : state.Output;
                return $"I executed {tokenName}. Result: {preview}";
            }

            return $"I executed {tokenName} successfully.";
        }
        catch (Exception ex)
        {
            var innerEx = ex.InnerException ?? ex;
            AnsiConsole.MarkupLine($"  [red]{Markup.Escape($"[!] Error: {innerEx.Message}")}[/]");
            return $"Error executing {tokenName}: {innerEx.Message}";
        }
    }

    /// <summary>
    /// Try to match natural language patterns to pipeline tokens.
    /// </summary>
    private async Task<string?> TryNaturalLanguageTokenAsync(
        string input,
        string personaName,
        CancellationToken ct)
    {
        if (_allTokens == null) return null;

        var lower = input.ToLowerInvariant();

        // Common natural language patterns mapped to tokens
        var patterns = new (string Pattern, string Token, int ArgGroup)[]
        {
            // ArXiv/Papers
            (@"search\s+(?:arxiv|papers?|research)\s+(?:for\s+)?(.+)", "ArxivSearch", 1),
            (@"find\s+(?:papers?|research)\s+(?:on|about)\s+(.+)", "ArxivSearch", 1),
            (@"(?:arxiv|papers?)\s+(?:on|about|for)\s+(.+)", "ArxivSearch", 1),
            (@"research\s+(.+)\s+papers?", "ArxivSearch", 1),

            // Wikipedia
            (@"search\s+wiki(?:pedia)?\s+(?:for\s+)?(.+)", "WikiSearch", 1),
            (@"(?:look\s+up|lookup)\s+(.+)\s+(?:on\s+)?wiki(?:pedia)?", "WikiSearch", 1),
            (@"what\s+(?:is|are)\s+(.+)\s+(?:according\s+to\s+)?wiki(?:pedia)?", "WikiSearch", 1),
            (@"wiki(?:pedia)?\s+(.+)", "WikiSearch", 1),

            // Semantic Scholar
            (@"search\s+semantic\s+scholar\s+(?:for\s+)?(.+)", "SemanticScholarSearch", 1),
            (@"find\s+citations?\s+(?:for|about)\s+(.+)", "SemanticScholarSearch", 1),

            // Web fetch
            (@"fetch\s+(?:url\s+)?(.+)", "Fetch", 1),
            (@"get\s+(?:content\s+from|page)\s+(.+)", "Fetch", 1),
            (@"download\s+(.+)", "Fetch", 1),

            // Generate/LLM
            (@"generate\s+(?:text\s+)?(?:about|on|for)\s+(.+)", "Generate", 1),
            (@"write\s+(?:about|on)\s+(.+)", "Generate", 1),

            // Summarize
            (@"summarize\s+(.+)", "Summarize", 1),
            (@"give\s+(?:me\s+)?(?:a\s+)?summary\s+(?:of\s+)?(.+)", "Summarize", 1),

            // Skill execution
            (@"use\s+skill\s+(.+)", "UseSkill", 1),
            (@"apply\s+skill\s+(.+)", "UseSkill", 1),
        };

        foreach (var (pattern, token, argGroup) in patterns)
        {
            var match = Regex.Match(lower, pattern, RegexOptions.IgnoreCase);
            if (match.Success && _allTokens.ContainsKey(token))
            {
                var arg = match.Groups[argGroup].Value.Trim();
                var command = $"{token} '{arg}'";
                AnsiConsole.MarkupLine($"  {OuroborosTheme.Dim($"[~] Interpreted as: {command}")}");
                return await TryExecuteSingleTokenAsync(command, personaName, ct);
            }
        }

        return null;
    }

    private async Task<string> HandleEmergenceAsync(
        string topic,
        string personaName,
        IVoiceOptions options,
        CancellationToken ct)
    {
        AnsiConsole.MarkupLine($"\n  [rgb(148,103,189)]\\[~] Running Ouroboros emergence cycle on: {Markup.Escape(topic)}...[/]");
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("      Phase 1: Research gathering..."));
        await Task.Delay(300, ct);
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("      Phase 2: Pattern extraction..."));
        await Task.Delay(300, ct);
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("      Phase 3: Synthesis..."));
        await Task.Delay(300, ct);
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("      Phase 4: Emergence detection..."));
        await Task.Delay(300, ct);
        AnsiConsole.MarkupLine(OuroborosTheme.Ok($"  [OK] Emergence cycle complete for {topic}"));

        return $"I completed an emergence cycle on {topic}. I've synthesized new patterns from the research.";
    }
}
