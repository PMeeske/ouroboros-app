namespace Ouroboros.Application.Configuration;

/// <summary>
/// Configuration for directory-based document ingestion.
/// </summary>
public record DirectoryIngestionConfig
{
    /// <summary>Root directory path to ingest.</summary>
    public required string Root { get; init; }
    
    /// <summary>Whether to recursively scan subdirectories.</summary>
    public bool Recursive { get; init; } = true;
    
    /// <summary>File extensions to include (e.g., [".cs", ".md"]).</summary>
    public IReadOnlyList<string> Extensions { get; init; } = Array.Empty<string>();
    
    /// <summary>Directory names to exclude from scanning.</summary>
    public IReadOnlyList<string> ExcludeDirectories { get; init; } = Array.Empty<string>();
    
    /// <summary>File patterns to match (e.g., ["*.cs", "*.md"]).</summary>
    public IReadOnlyList<string> Patterns { get; init; } = new[] { "*" };
    
    /// <summary>Maximum file size in bytes (0 = unlimited).</summary>
    public long MaxFileBytes { get; init; }
    
    /// <summary>Chunk size for text splitting.</summary>
    public int ChunkSize { get; init; } = DefaultIngestionSettings.ChunkSize;
    
    /// <summary>Chunk overlap for text splitting.</summary>
    public int ChunkOverlap { get; init; } = DefaultIngestionSettings.ChunkOverlap;
    
    /// <summary>Batch size for vector addition (0 = add all at once).</summary>
    public int BatchSize { get; init; } = 0;
}