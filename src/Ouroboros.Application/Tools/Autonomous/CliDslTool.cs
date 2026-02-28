// <copyright file="CliDslTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Tool for executing CLI DSL pipeline expressions.
/// Provides access to the Ouroboros pipeline DSL for reasoning, ingestion, and processing.
/// </summary>
public class CliDslTool : ITool
{
    /// <inheritdoc/>
    public string Name => "cli_dsl";

    /// <inheritdoc/>
    public string Description => "Execute a CLI DSL pipeline expression. " +
        "Available commands: SetTopic('x'), SetPrompt('x'), UseDraft, UseCritique, UseImprove, " +
        "UseRefinementLoop, MeTTaAtom('x'), MeTTaQuery('x'), and more. " +
        "Chain with | operator. Example: SetTopic('AI') | UseDraft | UseCritique";

    /// <inheritdoc/>
    public string? JsonSchema => """
{
  "type": "object",
  "properties": {
    "dsl": {
      "type": "string",
      "description": "The DSL pipeline expression to execute"
    },
    "explain": {
      "type": "boolean",
      "description": "If true, explain the pipeline without executing"
    },
    "list": {
      "type": "boolean",
      "description": "If true, list all available DSL tokens"
    }
  },
  "required": ["dsl"]
}
""";

    private readonly IAutonomousToolContext _ctx;
    public CliDslTool(IAutonomousToolContext context) => _ctx = context;
    public CliDslTool() : this(AutonomousTools.DefaultContext) { }

    // Shared state for pipeline continuity (injected by OuroborosAgent)
    /// <summary>
    /// The shared CLI pipeline state. Must be set before execution for full functionality.
    /// Delegates to <see cref="IAutonomousToolContext.PipelineState"/>.
    /// </summary>
    public static CliPipelineState? SharedState
    {
        get => AutonomousTools.DefaultContext.PipelineState;
        set => AutonomousTools.DefaultContext.PipelineState = value;
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        string dsl = input.Trim();
        bool explain = false;
        bool list = false;

        // Try to parse JSON input
        try
        {
            using var doc = JsonDocument.Parse(input);
            if (doc.RootElement.TryGetProperty("dsl", out var dslEl))
                dsl = dslEl.GetString() ?? dsl;
            if (doc.RootElement.TryGetProperty("explain", out var explainEl))
                explain = explainEl.GetBoolean();
            if (doc.RootElement.TryGetProperty("list", out var listEl))
                list = listEl.GetBoolean();
        }
        catch (System.Text.Json.JsonException) { /* Use raw input as DSL */ }

        // Handle list request
        if (list || dsl.Equals("list", StringComparison.OrdinalIgnoreCase) ||
            dsl.Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            return Result<string, string>.Success(GetAvailableTokens());
        }

        // Handle explain request
        if (explain || dsl.StartsWith("explain ", StringComparison.OrdinalIgnoreCase))
        {
            string toExplain = explain ? dsl : dsl.Substring(8).Trim();
            string explanation = PipelineDsl.Explain(toExplain);
            return Result<string, string>.Success(explanation);
        }

        if (string.IsNullOrWhiteSpace(dsl))
            return Result<string, string>.Failure("No DSL expression provided. Use 'list' to see available tokens.");

        // Check if we have a shared state
        if (_ctx.PipelineState == null)
        {
            // No state available - just explain what would happen
            string explanation = PipelineDsl.Explain(dsl);
            return Result<string, string>.Success(
                $"Pipeline explained (no execution context):\n\n{explanation}\n\n" +
                "Note: Full execution requires an active Ouroboros session with LLM connected.");
        }

        try
        {
            // Build and execute the pipeline
            var step = PipelineDsl.Build(dsl);
            var state = await step(_ctx.PipelineState);
            _ctx.PipelineState = state;

            // Build result summary
            var result = new StringBuilder();
            result.AppendLine($"\u2713 Executed: `{dsl}`\n");

            // Show any reasoning output
            var reasoningSteps = state.Branch.Events
                .OfType<ReasoningStep>()
                .TakeLast(3);

            foreach (var rs in reasoningSteps)
            {
                result.AppendLine($"**{rs.Kind}:**");
                string content = rs.State?.Text ?? "";
                if (content.Length > 500)
                    content = content[..500] + "...";
                result.AppendLine(content);
                result.AppendLine();
            }

            // Show current state info
            if (!string.IsNullOrWhiteSpace(state.Topic))
                result.AppendLine($"**Topic:** {state.Topic}");
            if (!string.IsNullOrWhiteSpace(state.Prompt))
                result.AppendLine($"**Prompt:** {state.Prompt}");
            if (!string.IsNullOrWhiteSpace(state.Output))
            {
                string output = state.Output.Length > 1000 ? state.Output[..1000] + "..." : state.Output;
                result.AppendLine($"\n**Output:**\n{output}");
            }

            // Show recent event count
            int eventCount = state.Branch.Events.Count();
            result.AppendLine($"\n**Pipeline events:** {eventCount}");

            return Result<string, string>.Success(result.ToString().Trim());
        }
        catch (InvalidOperationException ex)
        {
            return Result<string, string>.Failure($"DSL execution failed: {ex.Message}");
        }
        catch (KeyNotFoundException ex)
        {
            return Result<string, string>.Failure($"DSL execution failed: {ex.Message}");
        }
    }

    private static string GetAvailableTokens()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Available CLI DSL Tokens\n");

        sb.AppendLine("## Core Operations");
        sb.AppendLine("- `SetTopic('x')` - Set the current topic");
        sb.AppendLine("- `SetPrompt('x')` - Set the prompt text");
        sb.AppendLine("- `SetQuery('x')` - Set the search query");
        sb.AppendLine("- `SetSource('path')` - Set source directory");
        sb.AppendLine();

        sb.AppendLine("## Reasoning Pipeline");
        sb.AppendLine("- `UseDraft` - Generate initial draft");
        sb.AppendLine("- `UseCritique` - Critique the current draft");
        sb.AppendLine("- `UseImprove` / `UseFinal` - Improve based on critique");
        sb.AppendLine("- `UseRefinementLoop` - Full draft-critique-improve cycle");
        sb.AppendLine("- `UseSelfCritique` - Self-critique reasoning");
        sb.AppendLine("- `UseStreamingDraft` - Streaming draft generation");
        sb.AppendLine("- `UseStreamingSelfCritique` - Streaming self-critique");
        sb.AppendLine();

        sb.AppendLine("## Ingestion");
        sb.AppendLine("- `UseDir('path')` - Ingest directory contents");
        sb.AppendLine("- `ReadFile('path')` - Read file content");
        sb.AppendLine();

        sb.AppendLine("## MeTTa Knowledge Base");
        sb.AppendLine("- `MeTTaAtom('x')` - Create an atom");
        sb.AppendLine("- `MeTTaFact('x')` - Assert a fact");
        sb.AppendLine("- `MeTTaRule('x')` - Define a rule");
        sb.AppendLine("- `MeTTaQuery('x')` - Query the KB");
        sb.AppendLine("- `MeTTaConcept('x')` - Create a concept");
        sb.AppendLine("- `MeTTaLink('x y')` - Link atoms");
        sb.AppendLine("- `MeTTaIntrospect` - Show KB status");
        sb.AppendLine("- `MeTTaReset` - Clear the KB");
        sb.AppendLine();

        sb.AppendLine("## Examples");
        sb.AppendLine("```");
        sb.AppendLine("SetTopic('functional programming') | UseDraft | UseCritique");
        sb.AppendLine("SetPrompt('Explain monads') | UseRefinementLoop");
        sb.AppendLine("MeTTaAtom('concept1') | MeTTaAtom('concept2') | MeTTaLink('concept1 concept2')");
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("Chain commands with `|` operator.");

        return sb.ToString();
    }

    /// <summary>
    /// Resets the shared pipeline state.
    /// </summary>
    public static void ResetState()
    {
        AutonomousTools.DefaultContext.PipelineState = null;
    }
}
