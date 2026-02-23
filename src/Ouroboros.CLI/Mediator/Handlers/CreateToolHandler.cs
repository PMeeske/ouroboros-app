using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="CreateToolRequest"/>.
/// Dynamically creates a tool via the tool factory and registers it with the agent.
/// </summary>
public sealed class CreateToolHandler : IRequestHandler<CreateToolRequest, string>
{
    private readonly OuroborosAgent _agent;

    public CreateToolHandler(OuroborosAgent agent) => _agent = agent;

    public async Task<string> Handle(CreateToolRequest request, CancellationToken ct)
    {
        var toolName = request.ToolName;

        if (string.IsNullOrWhiteSpace(toolName))
            return "What kind of tool should I create?";

        var toolFactory = _agent.ToolsSub.ToolFactory;

        if (toolFactory == null)
            return "I need an LLM connection to create new tools.";

        try
        {
            var result = await toolFactory.CreateToolAsync(toolName, $"A tool for {toolName}");
            return result.Match(
                tool =>
                {
                    _agent.AddToolAndRefreshLlm(tool);
                    return $"Done! I created a '{toolName}' tool. You can now use it.";
                },
                error => $"I couldn't create that tool: {error}");
        }
        catch (Exception ex)
        {
            return $"I couldn't create that tool: {ex.Message}";
        }
    }
}
