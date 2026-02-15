using Microsoft.Extensions.Logging;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Setup;

namespace Ouroboros.CLI.Services;

/// <summary>
/// Production implementation of <see cref="IOuroborosAgentService"/>.
/// Delegates to <see cref="AgentBootstrapper"/> for configuration loading,
/// static provider setup, and agent creation, then runs the agent loop.
/// </summary>
public class OuroborosAgentService : IOuroborosAgentService
{
    private readonly ILogger<OuroborosAgentService> _logger;

    public OuroborosAgentService(ILogger<OuroborosAgentService> logger)
    {
        _logger = logger;
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

        // 2. Create the agent from the fully mapped config
        await using var agent = await AgentBootstrapper.CreateAgentAsync(config);

        _logger.LogInformation("Agent initialized, entering main loop");

        // 3. Run the agent's main interaction loop
        await agent.RunAsync();

        _logger.LogInformation("Agent session completed");
    }
}
