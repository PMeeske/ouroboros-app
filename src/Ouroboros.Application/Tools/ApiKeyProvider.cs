using Microsoft.Extensions.Configuration;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Provides access to API keys from configuration or environment.
/// </summary>
public static class ApiKeyProvider
{
    private static IConfiguration? _configuration;

    /// <summary>
    /// Sets the configuration instance for API key resolution.
    /// </summary>
    public static void SetConfiguration(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Gets an API key from configuration (user secrets) or environment variable.
    /// </summary>
    public static string? GetApiKey(string keyName)
    {
        // Try configuration first (includes user secrets)
        string? key = _configuration?[$"ApiKeys:{keyName}"];
        if (!string.IsNullOrWhiteSpace(key))
            return key;

        // Fall back to environment variable (e.g., FIRECRAWL_API_KEY)
        string envVarName = $"{keyName.ToUpperInvariant()}_API_KEY";
        return Environment.GetEnvironmentVariable(envVarName);
    }
}