using System.Text;
using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="DagCommandRequest"/>.
/// Extracted from <c>OuroborosAgent.DagCommandAsync</c>.
/// </summary>
public sealed class DagCommandHandler : IRequestHandler<DagCommandRequest, string>
{
    private readonly OuroborosAgent _agent;

    public DagCommandHandler(OuroborosAgent agent) => _agent = agent;

    public async Task<string> Handle(DagCommandRequest request, CancellationToken cancellationToken)
    {
        var subCommand = request.SubCommand;

        try
        {
            var dagOpts = new DagOptions
            {
                Command = subCommand?.ToLowerInvariant().Trim() ?? "show"
            };

            var originalOut = Console.Out;
            try
            {
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);
                    await DagCommands.RunDagAsync(dagOpts);
                    return writer.ToString();
                }
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        catch (Exception ex)
        {
            return $"DAG command error: {ex.Message}";
        }
    }
}
