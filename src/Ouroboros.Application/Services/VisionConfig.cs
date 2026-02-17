namespace Ouroboros.Application.Services;

/// <summary>
/// Configuration for the vision service.
/// </summary>
public class VisionConfig
{
    public VisionBackend Backend { get; set; } = VisionBackend.Ollama;
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";
    public string OllamaVisionModel { get; set; } = "llava:latest";
    public string? OpenAIApiKey { get; set; }
    public string OpenAIVisionModel { get; set; } = "gpt-4-vision-preview";
}