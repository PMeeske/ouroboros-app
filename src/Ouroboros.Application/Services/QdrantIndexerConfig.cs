namespace Ouroboros.Application.Services;

/// <summary>
/// Configuration for the Qdrant self-indexer.
/// </summary>
public sealed record QdrantIndexerConfig
{
    /// <summary>Qdrant gRPC endpoint.</summary>
    public string QdrantEndpoint { get; init; } = "http://localhost:6334";

    /// <summary>Collection name for indexed content.</summary>
    public string CollectionName { get; init; } = "ouroboros_selfindex";

    /// <summary>Collection for file hashes (for incremental updates).</summary>
    public string HashCollectionName { get; init; } = "ouroboros_filehashes";

    /// <summary>Root paths to index.</summary>
    public List<string> RootPaths { get; init; } = new();

    /// <summary>File extensions to index.</summary>
    public HashSet<string> Extensions { get; init; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".py", ".js", ".ts", ".json", ".md", ".txt", ".yaml", ".yml",
        ".xml", ".html", ".css", ".sql", ".sh", ".ps1", ".bat", ".cmd",
        ".config", ".csproj", ".sln", ".fsproj", ".vbproj", ".props", ".targets"
    };

    /// <summary>Directories to exclude.</summary>
    public HashSet<string> ExcludeDirectories { get; init; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "node_modules", ".git", ".vs", ".vscode", ".idea",
        "packages", "TestResults", "dist", "build", "out", ".next",
        "__pycache__", ".pytest_cache", "coverage", ".nyc_output"
    };

    /// <summary>Chunk size for text splitting.</summary>
    public int ChunkSize { get; init; } = 1000;

    /// <summary>Chunk overlap.</summary>
    public int ChunkOverlap { get; init; } = 200;

    /// <summary>Max file size to index in bytes.</summary>
    public long MaxFileSize { get; init; } = 1024 * 1024; // 1MB

    /// <summary>Batch size for upserts.</summary>
    public int BatchSize { get; init; } = 50;

    /// <summary>Vector dimensions (nomic-embed-text default).</summary>
    public int VectorSize { get; init; } = 768;

    /// <summary>Enable file watcher for live incremental updates.</summary>
    public bool EnableFileWatcher { get; init; } = true;

    /// <summary>Debounce delay for file changes in milliseconds.</summary>
    public int FileWatcherDebounceMs { get; init; } = 1000;
}