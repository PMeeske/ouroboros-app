using CommandLine;

namespace Ouroboros.Options;

/// <summary>
/// Base class for options that use AI models.
/// </summary>
public abstract class BaseModelOptions
{
    [Option("model", Required = false, Default = "deepseek-v3.1:671b-cloud", HelpText = "Model to use")]
    public string Model { get; set; } = "deepseek-v3.1:671b-cloud";

    [Option("embed", Required = false, Default = "nomic-embed-text", HelpText = "Embedding model")]
    public string Embed { get; set; } = "nomic-embed-text";

    [Option("temperature", Required = false, Default = 0.7, HelpText = "Temperature for generation")]
    public double Temperature { get; set; } = 0.7;

    [Option("max-tokens", Required = false, Default = 2000, HelpText = "Maximum tokens to generate")]
    public int MaxTokens { get; set; } = 2000;

    [Option("timeout", Required = false, Default = 120, HelpText = "Timeout in seconds")]
    public int TimeoutSeconds { get; set; } = 120;

    [Option("debug", Required = false, Default = false, HelpText = "Enable debug output")]
    public bool Debug { get; set; }

    [Option("endpoint", Required = false, HelpText = "Remote endpoint URL (e.g., https://api.ollama.com)")]
    public string? Endpoint { get; set; }

    [Option("api-key", Required = false, HelpText = "API key for remote endpoint")]
    public string? ApiKey { get; set; }

    [Option("endpoint-type", Required = false, Default = "auto", HelpText = "Endpoint type: auto, ollama-cloud, openai, litellm, github-models")]
    public string EndpointType { get; set; } = "auto";
}