namespace Ouroboros.Application.Personality;

/// <summary>
/// Executor that can create files and execute tools based on intention thoughts.
/// Enables the AI to take real actions when appropriate.
/// </summary>
public sealed class ActionExecutionExecutor : IBackgroundOperationExecutor
{
    private readonly Func<string, string, CancellationToken, Task<bool>>? _fileCreator;
    private readonly Func<string, Dictionary<string, object>, CancellationToken, Task<object?>>? _toolExecutor;

    public string Name => "ActionExecution";
    public IReadOnlyList<string> SupportedOperations => ["file_creation", "tool_execution", "command_execution"];

    public ActionExecutionExecutor(
        Func<string, string, CancellationToken, Task<bool>>? fileCreator = null,
        Func<string, Dictionary<string, object>, CancellationToken, Task<object?>>? toolExecutor = null)
    {
        _fileCreator = fileCreator;
        _toolExecutor = toolExecutor;
    }

    public bool ShouldExecute(InnerThoughtType thoughtType, BackgroundOperationContext context)
    {
        // Execute when we have intention thoughts and available tools
        return thoughtType == InnerThoughtType.Intention &&
               (context.AvailableTools.Count > 0 || _fileCreator != null);
    }

    public async Task<BackgroundOperationResult?> ExecuteAsync(
        InnerThought thought,
        BackgroundOperationContext context,
        CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Analyze thought content to determine action
        var content = thought.Content.ToLowerInvariant();

        // Check for file creation intent
        if (_fileCreator != null && (content.Contains("create") || content.Contains("file") || content.Contains("build")))
        {
            // For now, just signal capability - actual creation requires explicit request
            sw.Stop();
            return new BackgroundOperationResult(
                "capability_ready",
                "file_creation",
                true,
                "File creation capability is available and ready",
                new { CanCreate = true, Trigger = thought.Content },
                sw.Elapsed,
                thought.Type);
        }

        // Check for tool execution intent
        if (_toolExecutor != null && context.AvailableTools.Count > 0)
        {
            var matchingTool = context.AvailableTools
                .FirstOrDefault(t => content.Contains(t.ToLowerInvariant()));

            if (!string.IsNullOrEmpty(matchingTool))
            {
                sw.Stop();
                return new BackgroundOperationResult(
                    "capability_ready",
                    matchingTool,
                    true,
                    $"Tool '{matchingTool}' is ready for execution",
                    new { Tool = matchingTool, Ready = true },
                    sw.Elapsed,
                    thought.Type);
            }
        }

        sw.Stop();
        return null;
    }
}