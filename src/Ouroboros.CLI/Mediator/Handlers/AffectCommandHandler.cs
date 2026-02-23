using System.Text;
using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="AffectCommandRequest"/>.
/// Extracted from <c>OuroborosAgent.AffectCommandAsync</c>.
/// </summary>
public sealed class AffectCommandHandler : IRequestHandler<AffectCommandRequest, string>
{
    private readonly OuroborosAgent _agent;

    public AffectCommandHandler(OuroborosAgent agent) => _agent = agent;

    public async Task<string> Handle(AffectCommandRequest request, CancellationToken cancellationToken)
    {
        var subCommand = request.SubCommand;

        try
        {
            var affectOpts = new AffectOptions
            {
                Command = subCommand?.ToLowerInvariant().Trim() ?? "status"
            };

            var originalOut = Console.Out;
            try
            {
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);
                    await AffectCommands.RunAffectAsync(affectOpts);
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
            return $"Affect command error: {ex.Message}";
        }
    }
}
