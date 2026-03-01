using System.CommandLine;
using Ouroboros.Application.Configuration;

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
        DefaultValueFactory = _ => DefaultEndpoints.QdrantGrpc
    };

    public void AddToCommand(Command command)
    {
        command.Add(EmbedModelOption);
        command.Add(QdrantEndpointOption);
    }
}