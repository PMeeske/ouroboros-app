using Microsoft.Extensions.Logging;

namespace Ouroboros.CLI.Services;

/// <summary>
/// Implementation of ISkillsService backed by SkillCliSteps and the real pipeline token registry.
/// </summary>
public partial class SkillsService : ISkillsService
{
    private readonly ILogger<SkillsService> _logger;
    private static readonly Lazy<HttpClient> _httpClient = new(() => new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(30),
        DefaultRequestHeaders = { { "User-Agent", "Ouroboros/1.0 (Research Pipeline)" } }
    });

    public SkillsService(ILogger<SkillsService> logger)
    {
        _logger = logger;
    }

    public Task<IEnumerable<SkillInfo>> ListSkillsAsync()
    {
        _logger.LogInformation("Listing skills from SkillCliSteps pipeline tokens");

        // Gather real pipeline tokens discovered at runtime by SkillCliSteps
        var tokens = SkillCliSteps.GetAllPipelineTokens();

        var skills = tokens.Values
            .DistinctBy(t => t.PrimaryName)
            .Select(t => new SkillInfo
            {
                Name = t.PrimaryName,
                Description = t.Description,
                SuccessRate = 1.0f // pipeline tokens don't track individual success rate
            })
            .OrderBy(s => s.Name);

        return Task.FromResult<IEnumerable<SkillInfo>>(skills);
    }

    public async Task<string> FetchAndExtractSkillAsync(string researchQuery)
    {
        _logger.LogInformation("Fetching research content: {ResearchQuery}", researchQuery);

        try
        {
            // Use the same HTTP client that SkillCliSteps.Fetch uses under the hood
            if (Uri.TryCreate(researchQuery, UriKind.Absolute, out var uri)
                && (uri.Scheme == "http" || uri.Scheme == "https"))
            {
                var content = await _httpClient.Value.GetStringAsync(uri);
                if (content.Length > 50_000) content = content[..50_000] + "\n...[truncated]";
                return content;
            }

            return $"'{researchQuery}' is not a valid absolute URL. " +
                   "Use the pipeline command with ArxivSearch, WikiSearch, or Fetch tokens for full research capabilities.";
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Fetch failed for {Query}", researchQuery);
            return $"Fetch failed: {ex.Message}";
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Fetch failed for {Query}", researchQuery);
            return $"Fetch failed: {ex.Message}";
        }
    }
}