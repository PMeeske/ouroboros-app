using Ouroboros.Application.Utilities;

namespace Ouroboros.Application.Configuration;

/// <summary>
/// Builder for DirectoryIngestionConfig.
/// </summary>
public static class DirectoryIngestionConfigBuilder
{
    /// <summary>
    /// Parses configuration from arguments string.
    /// </summary>
    /// <param name="args">The arguments string.</param>
    /// <param name="defaultRoot">The default root directory.</param>
    /// <returns>A Result containing the configuration.</returns>
    public static Result<DirectoryIngestionConfig> Parse(
        string? args,
        string defaultRoot)
    {
        var defaults = new DirectoryIngestionConfig { Root = defaultRoot };
        
        return ConfigParser.Parse(args, defaults, (dict, config) =>
        {
            string root = dict.GetValueOrDefault("root", config.Root);
            bool recursive = !dict.ContainsKey("norec");
            
            List<string> extensions = dict.TryGetValue("ext", out string? extStr)
                ? extStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                : config.Extensions.ToList();
                
            List<string> excludeDirs = dict.TryGetValue("exclude", out string? excStr)
                ? excStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                : config.ExcludeDirectories.ToList();
                
            List<string> patterns = dict.TryGetValue("pattern", out string? patStr)
                ? patStr.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                : config.Patterns.ToList();
                
            long maxBytes = dict.TryGetValue("max", out string? maxStr) && long.TryParse(maxStr, out long mb)
                ? mb
                : config.MaxFileBytes;
                
            int batchSize = dict.TryGetValue("batch", out string? batchStr) && int.TryParse(batchStr, out int bs)
                ? bs
                : config.BatchSize;

            if (!Directory.Exists(root))
            {
                return Result<DirectoryIngestionConfig>.Failure($"Directory not found: {root}");
            }

            return Result<DirectoryIngestionConfig>.Success(new DirectoryIngestionConfig
            {
                Root = Path.GetFullPath(root),
                Recursive = recursive,
                Extensions = extensions,
                ExcludeDirectories = excludeDirs,
                Patterns = patterns.Count == 0 ? new[] { "*" } : patterns,
                MaxFileBytes = maxBytes,
                BatchSize = batchSize,
                ChunkSize = config.ChunkSize,
                ChunkOverlap = config.ChunkOverlap
            });
        });
    }

    /// <summary>
    /// Parses configuration for batched ingestion.
    /// </summary>
    /// <param name="args">The arguments string.</param>
    /// <param name="defaultRoot">The default root directory.</param>
    /// <returns>A Result containing the configuration.</returns>
    public static Result<DirectoryIngestionConfig> ParseBatched(
        string? args,
        string defaultRoot)
    {
        // Reuse the main parser but ensure batch size is set if not provided
        return Parse(args, defaultRoot).Map(config => 
        {
            if (config.BatchSize == 0)
            {
                return config with { BatchSize = DefaultIngestionSettings.DefaultBatchSize };
            }
            return config;
        });
    }
}