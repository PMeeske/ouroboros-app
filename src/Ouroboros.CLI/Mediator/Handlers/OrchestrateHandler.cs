using System.Text;
using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="OrchestrateRequest"/>.
/// Extracted from <c>OuroborosAgent.OrchestrateAsync</c>.
/// </summary>
public sealed class OrchestrateHandler : IRequestHandler<OrchestrateRequest, string>
{
    private readonly OuroborosAgent _agent;

    public OrchestrateHandler(OuroborosAgent agent) => _agent = agent;

    public async Task<string> Handle(OrchestrateRequest request, CancellationToken cancellationToken)
    {
        var goal = request.Goal;

        if (string.IsNullOrWhiteSpace(goal))
            return "What would you like me to orchestrate?";

        try
        {
            var orchestratorOpts = new OrchestratorOptions
            {
                Goal = goal,
                Model = "llama3",
                Temperature = 0.7,
                MaxTokens = 4096,
                TimeoutSeconds = 300,
                Voice = false,
                Debug = false,
                Culture = Thread.CurrentThread.CurrentCulture.Name
            };

            var originalOut = Console.Out;
            try
            {
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);
                    await OrchestratorCommands.RunOrchestratorAsync(orchestratorOpts);
                    return writer.ToString();
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return $"Orchestration error: {ex.Message}";
        }
    }
}
