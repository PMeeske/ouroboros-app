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