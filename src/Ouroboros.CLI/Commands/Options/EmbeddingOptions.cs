using System.CommandLine;

namespace Ouroboros.CLI.Commands.Options;

/// <summary>
/// Embedding model and vector store configuration.
/// Shared by: ask, pipeline, ouroboros, skills.
/// </summary>
public sealed class EmbeddingOptions : IComposableOptions
{
    public Option<string> EmbedModelOption { get; } = new("--embed-model")
    {
        Description = "Embedding model name",
        DefaultValueFactory = _ => "nomic-embed-text"
    };

    public Option<string> QdrantEndpointOption { get; } = new("--qdrant")
    {
        Description = "Qdrant endpoint for persistent memory",
        DefaultValueFactory = _ => "http://localhost:6334"
    };

    public void AddToCommand(Command command)
    {
        command.Add(EmbedModelOption);
        command.Add(QdrantEndpointOption);
    }
}