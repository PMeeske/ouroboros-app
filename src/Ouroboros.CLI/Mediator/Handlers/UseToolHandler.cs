using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="UseToolRequest"/>.
/// Invokes a tool by name (exact or partial match) with the given input.
/// </summary>
public sealed class UseToolHandler : IRequestHandler<UseToolRequest, string>
{
    private readonly OuroborosAgent _agent;

    public UseToolHandler(OuroborosAgent agent) => _agent = agent;

    public async Task<string> Handle(UseToolRequest request, CancellationToken ct)
    {
        var toolName = request.ToolName;
        var input = request.Input;
        var tools = _agent.ToolsSub.Tools;

        var tool = tools.Get(toolName) ?? tools.All.FirstOrDefault(t =>
            t.Name.Contains(toolName, StringComparison.OrdinalIgnoreCase));

        if (tool == null)
            return $"I don't have a '{toolName}' tool. Try 'list tools' to see what's available.";

        try
        {
            var result = await tool.InvokeAsync(input ?? "");
            return $"Result: {result}";
        }
        catch (Exception ex)
        {
            return $"The tool ran into an issue: {ex.Message}";
        }
    }
}
