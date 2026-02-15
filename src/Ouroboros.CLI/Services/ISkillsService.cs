namespace Ouroboros.CLI.Services;

/// <summary>
/// Service for handling skills commands
/// </summary>
public interface ISkillsService
{
    /// <summary>
    /// Lists all available skills
    /// </summary>
    Task<IEnumerable<SkillInfo>> ListSkillsAsync();
    
    /// <summary>
    /// Fetches research and extracts a skill
    /// </summary>
    Task<string> FetchAndExtractSkillAsync(string query);
}

/// <summary>
/// Skill information DTO
/// </summary>
public record SkillInfo(string Name, string Description, double SuccessRate);

/// <summary>
/// Implementation of skills service
/// </summary>
public class SkillsService : ISkillsService
{
    private readonly ILogger<SkillsService> _logger;
    
    public SkillsService(ILogger<SkillsService> logger)
    {
        _logger = logger;
    }
    
    public async Task<IEnumerable<SkillInfo>> ListSkillsAsync()
    {
        _logger.LogInformation("Listing skills");
        
        // TODO: Extract business logic from existing SkillsCommands
        
        return new List<SkillInfo>
        {
            new("LiteratureReview", "Synthesize research papers", 0.85),
            new("HypothesisGeneration", "Generate testable hypotheses", 0.78)
        };
    }
    
    public async Task<string> FetchAndExtractSkillAsync(string query)
    {
        _logger.LogInformation("Fetching research for: {Query}", query);
        
        // TODO: Extract business logic from existing SkillsCommands
        
        return $"Skill extracted from: {query}";
    }
}