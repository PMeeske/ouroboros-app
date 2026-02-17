namespace Ouroboros.Application.Integration;

/// <summary>Options for episodic memory configuration.</summary>
public sealed record EpisodicMemoryOptions(
    string VectorStoreType = "InMemory",
    int MaxEpisodes = 10000,
    double SimilarityThreshold = 0.7);