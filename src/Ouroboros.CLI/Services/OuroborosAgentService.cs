using Microsoft.Extensions.Logging;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Setup;

namespace Ouroboros.CLI.Services;

/// <summary>
/// Production implementation of <see cref="IOuroborosAgentService"/>.
/// Creates and initializes an <see cref="OuroborosAgent"/>, then calls
/// <see cref="OuroborosAgent.RunAsync"/> which owns the full session lifecycle:
/// ImmersiveMode as the foreground experience and RoomMode as ambient background presence.
/// </summary>
public class OuroborosAgentService : IOuroborosAgentService
{
    private readonly ILogger<OuroborosAgentService> _logger;
    private readonly IServiceProvider _hostServices;

    public OuroborosAgentService(
        ILogger<OuroborosAgentService> logger,
        IServiceProvider hostServices)
    {
        _logger = logger;
        _hostServices = hostServices;
    }

    /// <inheritdoc />
    public async Task RunAgentAsync(OuroborosConfig config, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting Ouroboros agent — persona={Persona}, model={Model}, endpoint={Endpoint}",
            config.Persona, config.Model, config.Endpoint);

        var configuration = AgentBootstrapper.LoadConfiguration();
        AgentBootstrapper.ApplyConfiguration(configuration);

        // Use the child-container DI path so subsystems + MediatR are properly registered.
        // ActivatorUtilities against the host container lacks subsystem registrations and
        // falls back to the legacy constructor which sets IMediator = null.
        var (agent, provider) = await AgentBootstrapper.CreateAgentWithDIAsync(config, _hostServices);

        try
        {
            await agent.RunAsync(cancellationToken);
        }
        finally
        {
            await agent.DisposeAsync();
            await provider.DisposeAsync();
            _logger.LogInformation("Agent session completed");
        }
    }
}
