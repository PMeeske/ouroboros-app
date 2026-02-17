namespace Ouroboros.Application.Agent;

/// <summary>
/// Configuration parsed from the AutoAgent pipeline token arguments.
/// </summary>
public sealed class AutoAgentConfig
{
    public string? Task { get; set; }
    public int MaxIterations { get; set; } = 15;

    /// <summary>
    /// Parses a semicolon-separated argument string into an <see cref="AutoAgentConfig"/>.
    /// Supports: <c>task text;maxIter=N</c>.
    /// </summary>
    public static AutoAgentConfig Parse(string? args)
    {
        var config = new AutoAgentConfig();

        if (string.IsNullOrWhiteSpace(args)) return config;

        // Remove quotes if present
        if (args.StartsWith("'") && args.EndsWith("'")) args = args[1..^1];
        if (args.StartsWith("\"") && args.EndsWith("\"")) args = args[1..^1];

        foreach (var part in args.Split(';'))
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("maxIter=", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(trimmed[8..], out var max))
                    config.MaxIterations = max;
            }
            else if (!trimmed.Contains('='))
            {
                config.Task = trimmed;
            }
        }

        return config;
    }
}