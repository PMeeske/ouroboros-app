namespace Ouroboros.Application.Configuration;

public record DivideAndConquerRagConfig
{
    public int RetrievalCount { get; init; } = 24;
    public int GroupSize { get; init; } = 6;
    public string Separator { get; init; } = "\n---\n";
    public string? CustomTemplate { get; init; }
    public string? FinalTemplate { get; init; }
    public bool StreamPartials { get; init; } = false;
}