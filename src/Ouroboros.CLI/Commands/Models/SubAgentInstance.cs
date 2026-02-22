using Ouroboros.Abstractions.Core;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// Represents a sub-agent instance for task delegation.
/// </summary>
public sealed class SubAgentInstance
{
    public string AgentId { get; }
    public string Name { get; }
    public HashSet<string> Capabilities { get; }
    private readonly IChatCompletionModel? _model;

    public SubAgentInstance(string agentId, string name, HashSet<string> capabilities, IChatCompletionModel? model)
    {
        AgentId = agentId;
        Name = name;
        Capabilities = capabilities;
        _model = model;
    }

    public async Task<string> ExecuteTaskAsync(string task, CancellationToken ct = default)
    {
        if (_model == null)
        {
            return $"[{Name}] No model available for execution.";
        }

        var prompt = $"You are {Name}, a specialized sub-agent with capabilities in: {string.Join(", ", Capabilities)}.\n\nTask: {task}\n\nProvide a focused, expert response:";
        return await _model.GenerateTextAsync(prompt, ct);
    }
}