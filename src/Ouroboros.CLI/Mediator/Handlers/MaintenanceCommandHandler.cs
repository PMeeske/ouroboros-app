using System.Text;
using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="MaintenanceCommandRequest"/>.
/// Extracted from <c>OuroborosAgent.MaintenanceCommandAsync</c>.
/// </summary>
public sealed class MaintenanceCommandHandler : IRequestHandler<MaintenanceCommandRequest, string>
{
    private readonly OuroborosAgent _agent;

    public MaintenanceCommandHandler(OuroborosAgent agent) => _agent = agent;

    public async Task<string> Handle(MaintenanceCommandRequest request, CancellationToken cancellationToken)
    {
        var subCommand = request.SubCommand;

        try
        {
            var maintenanceOpts = new MaintenanceOptions
            {
                Command = subCommand?.ToLowerInvariant().Trim() ?? "status"
            };

            var originalOut = Console.Out;
            try
            {
                using (var writer = new StringWriter())
                {
                    Console.SetOut(writer);
                    await MaintenanceCommands.RunMaintenanceAsync(maintenanceOpts);
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
            return $"Maintenance command error: {ex.Message}";
        }
    }
}
