using System.Text;
using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="NetworkCommandRequest"/>.
/// Extracted from <c>OuroborosAgent.NetworkCommandAsync</c>.
/// </summary>
public sealed class NetworkCommandHandler : IRequestHandler<NetworkCommandRequest, string>
{
    private readonly OuroborosAgent _agent;

    public NetworkCommandHandler(OuroborosAgent agent) => _agent = agent;

    public async Task<string> Handle(NetworkCommandRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var networkOpts = new NetworkOptions();

            var originalOut = Console.Out;
            try
            {
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);
                    await NetworkCommands.RunAsync(networkOpts);
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
            return $"Network command error: {ex.Message}";
        }
    }
}
