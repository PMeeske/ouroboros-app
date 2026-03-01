// <copyright file="ParallelToolsTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Parallel tool executor that runs multiple tools concurrently.
/// Overcomes sequential execution limitation.
/// </summary>
public class ParallelToolsTool : ITool
{
    private readonly IAutonomousToolContext _ctx;
    public ParallelToolsTool(IAutonomousToolContext context) => _ctx = context;
    public ParallelToolsTool() : this(AutonomousTools.DefaultContext) { }

    public string Name => "parallel_tools";
    public string Description => "Execute multiple tools in parallel. Overcomes sequential execution limit. Input: JSON {\"tools\":[{\"name\":\"...\",\"input\":\"...\"},...]]}";
    public string? JsonSchema => null;

    /// <summary>
    /// Delegate for executing a tool by name. Delegates to <see cref="IAutonomousToolContext.ExecuteToolFunction"/>.
    /// </summary>
    public static Func<string, string, CancellationToken, Task<string>>? ExecuteToolFunction
    {
        get => AutonomousTools.DefaultContext.ExecuteToolFunction;
        set => AutonomousTools.DefaultContext.ExecuteToolFunction = value;
    }

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(input);
            var toolsArray = doc.RootElement.GetProperty("tools");

            if (_ctx.ExecuteToolFunction == null)
                return Result<string, string>.Failure("Tool execution function not available.");

            var toolCalls = new List<(string name, string input)>();

            foreach (var tool in toolsArray.EnumerateArray())
            {
                var name = tool.GetProperty("name").GetString() ?? "";
                var toolInput = tool.TryGetProperty("input", out var inp) ? inp.ToString() : "{}";
                toolCalls.Add((name, toolInput));
            }

            if (toolCalls.Count == 0)
                return Result<string, string>.Failure("No tools specified.");

            if (toolCalls.Count > 10)
                return Result<string, string>.Failure("Maximum 10 parallel tools allowed.");

            // Execute all tools in parallel
            var tasks = toolCalls.Select(async tc =>
            {
                try
                {
                    var result = await _ctx.ExecuteToolFunction(tc.name, tc.input, ct);
                    return (tc.name, success: true, result);
                }
                catch (OperationCanceledException) { throw; }
                catch (InvalidOperationException ex)
                {
                    return (tc.name, success: false, result: ex.Message);
                }
            });

            var results = await Task.WhenAll(tasks);

            var sb = new StringBuilder();
            sb.AppendLine($"\u26a1 **Parallel Execution Complete** ({results.Length} tools)\n");

            foreach (var (name, success, result) in results)
            {
                sb.AppendLine($"### {name} {(success ? "\u2705" : "\u274c")}");
                sb.AppendLine(result.Substring(0, Math.Min(300, result.Length)));
                sb.AppendLine();
            }

            return Result<string, string>.Success(sb.ToString());
        }
        catch (JsonException ex)
        {
            return Result<string, string>.Failure($"Parallel execution failed: {ex.Message}");
        }
        catch (AggregateException ex)
        {
            return Result<string, string>.Failure($"Parallel execution failed: {ex.InnerException?.Message ?? ex.Message}");
        }
    }
}
