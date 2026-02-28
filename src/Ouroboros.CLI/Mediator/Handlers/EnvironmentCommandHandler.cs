using System.Text;
using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="EnvironmentCommandRequest"/>.
/// Extracted from <c>OuroborosAgent.EnvironmentCommandAsync</c>.
/// </summary>
public sealed class EnvironmentCommandHandler : IRequestHandler<EnvironmentCommandRequest, string>
{
    private readonly OuroborosAgent _agent;

    public EnvironmentCommandHandler(OuroborosAgent agent) => _agent = agent;

    public async Task<string> Handle(EnvironmentCommandRequest request, CancellationToken cancellationToken)
    {
        var subCommand = request.SubCommand;

        try
        {
            var envOpts = new EnvironmentOptions
            {
                Command = subCommand?.ToLowerInvariant().Trim() ?? "status"
            };

            var originalOut = Console.Out;
            try
            {
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);
                    await EnvironmentCommands.RunEnvironmentCommandAsync(envOpts);
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
            return $"Environment command error: {ex.Message}";
        }
    }
}
