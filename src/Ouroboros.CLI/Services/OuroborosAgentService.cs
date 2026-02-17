using Microsoft.Extensions.Logging;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Setup;

namespace Ouroboros.CLI.Services;

/// <summary>
/// Production implementation of <see cref="IOuroborosAgentService"/>.
/// Delegates to <see cref="AgentBootstrapper"/> for configuration loading,
/// static provider setup, and agent creation, then runs the agent loop.
/// Bridges host DI services into the agent's child container so that shared
/// infrastructure (IConfiguration, ILoggerFactory, ISpectreConsoleService)
/// flows through a single instance.
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
            "Starting Ouroboros agent — persona={Persona}, model={Model}, endpoint={Endpoint}, voice={Voice}",
            config.Persona, config.Model, config.Endpoint, config.Voice);

        // 1. Load & apply IConfiguration (appsettings, secrets, env vars)
        var configuration = AgentBootstrapper.LoadConfiguration();
        AgentBootstrapper.ApplyConfiguration(configuration);

        // 2. Create the agent via DI — pass the host's IServiceProvider so that
        //    shared services (IConfiguration, ILoggerFactory, ISpectreConsoleService)
        //    are forwarded into the child container instead of being duplicated.
        var (agent, provider) = await AgentBootstrapper.CreateAgentWithDIAsync(config, _hostServices);
        await using (provider)
        {
            _logger.LogInformation("Agent initialized via DI, entering main loop");

            // 3. Run the agent's main interaction loop
            await agent.RunAsync();

            _logger.LogInformation("Agent session completed");
        }
    }
}
