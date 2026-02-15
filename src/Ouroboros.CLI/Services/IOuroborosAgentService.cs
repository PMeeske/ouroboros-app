namespace Ouroboros.CLI.Services;

/// <summary>
/// Service for handling Ouroboros agent commands
/// </summary>
public interface IOuroborosAgentService
{
    /// <summary>
    /// Runs the Ouroboros agent
    /// </summary>
    Task RunAgentAsync(string persona);
}

/// <summary>
/// Implementation of Ouroboros agent service
/// </summary>
public class OuroborosAgentService : IOuroborosAgentService
{
    private readonly ILogger<OuroborosAgentService> _logger;
    
    public OuroborosAgentService(ILogger<OuroborosAgentService> logger)
    {
        _logger = logger;
    }
    
    public async Task RunAgentAsync(string persona)
    {
        _logger.LogInformation("Running Ouroboros agent with persona: {Persona}", persona);
        
        // TODO: Extract business logic from existing OuroborosCommands
        
        await Task.CompletedTask;
    }
}