namespace Ouroboros.Application.Personality;

/// <summary>
/// Executor that suggests tools and skills based on anticipatory thoughts.
/// Prepares action recommendations proactively.
/// </summary>
public sealed class AnticipatoryActionExecutor : IBackgroundOperationExecutor
{
    private readonly Func<string, List<string>, CancellationToken, Task<(string Action, string Reason)>>? _suggester;

    public string Name => "AnticipatoryAction";
    public IReadOnlyList<string> SupportedOperations => ["action_suggestion", "tool_recommendation"];

    public AnticipatoryActionExecutor(
        Func<string, List<string>, CancellationToken, Task<(string Action, string Reason)>>? suggester = null)
    {
        _suggester = suggester;
    }

    public bool ShouldExecute(InnerThoughtType thoughtType, BackgroundOperationContext context)
    {
        return (thoughtType == InnerThoughtType.Anticipatory || thoughtType == InnerThoughtType.Intention) &&
               !string.IsNullOrEmpty(context.LastUserMessage) &&
               (context.AvailableTools.Count > 0 || context.AvailableSkills.Count > 0);
    }

    public async Task<BackgroundOperationResult?> ExecuteAsync(
        InnerThought thought,
        BackgroundOperationContext context,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(context.LastUserMessage)) return null;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var allActions = context.AvailableTools.Concat(context.AvailableSkills).ToList();

        string suggestedAction;
        string reason;

        if (_suggester != null)
        {
            (suggestedAction, reason) = await _suggester(context.LastUserMessage, allActions, ct);
        }
        else
        {
            // Default: simple keyword matching
            (suggestedAction, reason) = SuggestAction(context.LastUserMessage, allActions);
        }

        sw.Stop();

        if (string.IsNullOrEmpty(suggestedAction))
        {
            return null;
        }

        return new BackgroundOperationResult(
            "action_suggestion",
            suggestedAction,
            true,
            reason,
            new Dictionary<string, object>
            {
                ["suggested_action"] = suggestedAction,
                ["reason"] = reason,
                ["available_actions"] = allActions,
                ["context"] = context.LastUserMessage
            },
            sw.Elapsed,
            thought.Type);
    }

    private static (string Action, string Reason) SuggestAction(string message, List<string> actions)
    {
        var messageLower = message.ToLowerInvariant();

        foreach (var action in actions)
        {
            var actionLower = action.ToLowerInvariant();
            if (messageLower.Contains(actionLower) || actionLower.Contains(messageLower.Split(' ').FirstOrDefault() ?? ""))
            {
                return (action, $"Direct match with user intent regarding '{action}'");
            }
        }

        // Pattern matching for common intents
        var patterns = new Dictionary<string[], string[]>
        {
            { ["search", "find", "look"], ["search", "query", "lookup", "find"] },
            { ["create", "make", "generate"], ["create", "generate", "build", "make"] },
            { ["analyze", "examine", "check"], ["analyze", "inspect", "evaluate", "check"] },
            { ["help", "assist", "support"], ["help", "assist", "guide", "support"] }
        };

        foreach (var (keywords, actionPatterns) in patterns)
        {
            if (keywords.Any(k => messageLower.Contains(k)))
            {
                var match = actions.FirstOrDefault(a =>
                    actionPatterns.Any(p => a.ToLowerInvariant().Contains(p)));
                if (match != null)
                {
                    return (match, $"Pattern match: user seems to want to {keywords[0]}");
                }
            }
        }

        return (string.Empty, string.Empty);
    }
}