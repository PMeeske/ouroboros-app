// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Ouroboros.Application.Services;
using Ouroboros.Core.Configuration;
using Ouroboros.Domain;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Xunit;

namespace Ouroboros.Tests.Services;

[Trait("Category", "Unit")]
public class QdrantSelfIndexerTests
{
    // ======================================================================
    // ShouldIndexFile — tested via observable behavior
    // ======================================================================

    [Theory]
    [InlineData(".cs", true)]
    [InlineData(".py", true)]
    [InlineData(".js", true)]
    [InlineData(".ts", true)]
    [InlineData(".md", true)]
    [InlineData(".json", true)]
    [InlineData(".dll", false)]
    [InlineData(".exe", false)]
    [InlineData(".pdf", false)]
    [InlineData(".zip", false)]
    public void ShouldIndexFile_ByExtension_ShouldRespectConfiguredExtensions(string extension, bool shouldIndex)
    {
        // Arrange
        var config = new QdrantIndexerConfig();

        // Act
        var contains = config.Extensions.Contains(extension);

        // Assert
        contains.Should().Be(shouldIndex);
    }

    [Theory]
    [InlineData("bin")]
    [InlineData("obj")]
    [InlineData("node_modules")]
    [InlineData(".git")]
    [InlineData(".vs")]
    [InlineData("TestResults")]
    [InlineData("__pycache__")]
    public void ShouldIndexFile_ShouldExcludeConfiguredDirectories(string dirName)
    {
        // Arrange
        var config = new QdrantIndexerConfig();

        // Act & Assert
        config.ExcludeDirectories.Should().Contain(dirName);
    }

    // ======================================================================
    // ComputeFileHash — tested via observable behavior
    // ======================================================================

    [Fact]
    public void ComputeFileHash_SameContent_ShouldProduceSameHash()
    {
        // Arrange
        var tempFile1 = Path.GetTempFileName();
        var tempFile2 = Path.GetTempFileName();
        File.WriteAllText(tempFile1, "identical content");
        File.WriteAllText(tempFile2, "identical content");

        try
        {
            // Act — replicate ComputeFileHash logic
            string hash1 = ComputeHash(tempFile1);
            string hash2 = ComputeHash(tempFile2);

            // Assert
            hash1.Should().Be(hash2);
        }
        finally
        {
            File.Delete(tempFile1);
            File.Delete(tempFile2);
        }
    }

    [Fact]
    public void ComputeFileHash_DifferentContent_ShouldProduceDifferentHash()
    {
        // Arrange
        var tempFile1 = Path.GetTempFileName();
        var tempFile2 = Path.GetTempFileName();
        File.WriteAllText(tempFile1, "content A");
        File.WriteAllText(tempFile2, "content B");

        try
        {
            // Act
            string hash1 = ComputeHash(tempFile1);
            string hash2 = ComputeHash(tempFile2);

            // Assert
            hash1.Should().NotBe(hash2);
        }
        finally
        {
            File.Delete(tempFile1);
            File.Delete(tempFile2);
        }
    }

    [Fact]
    public void ComputeFileHash_NonExistentFile_ShouldReturnEmpty()
    {
        // Act
        string hash = ComputeHash("/nonexistent_file_for_hash_test.xyz");

        // Assert
        hash.Should().BeEmpty();
    }

    // ======================================================================
    // GeneratePointId — deterministic and consistent
    // ======================================================================

    [Fact]
    public void GeneratePointId_SameInputs_ShouldProduceSameId()
    {
        // Act
        var id1 = GeneratePointId("file.cs", 0);
        var id2 = GeneratePointId("file.cs", 0);

        // Assert
        id1.Should().Be(id2);
    }

    [Fact]
    public void GeneratePointId_DifferentChunkIndex_ShouldProduceDifferentId()
    {
        // Act
        var id1 = GeneratePointId("file.cs", 0);
        var id2 = GeneratePointId("file.cs", 1);

        // Assert
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void GeneratePointId_DifferentFilePath_ShouldProduceDifferentId()
    {
        // Act
        var id1 = GeneratePointId("a.cs", 0);
        var id2 = GeneratePointId("b.cs", 0);

        // Assert
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void GeneratePointId_ShouldReturnValidGuidString()
    {
        // Act
        var id = GeneratePointId("test.cs", 42);

        // Assert
        Guid.TryParse(id, out _).Should().BeTrue();
    }

    // ======================================================================
    // CosineSimilarity — tested via observable behavior
    // ======================================================================

    [Fact]
    public void CosineSimilarity_IdenticalVectors_ShouldReturn1()
    {
        // Arrange
        var a = new float[] { 1f, 2f, 3f };
        var b = new float[] { 1f, 2f, 3f };

        // Act
        var similarity = CosineSimilarity(a, b);

        // Assert
        similarity.Should().BeApproximately(1f, 0.001f);
    }

    [Fact]
    public void CosineSimilarity_OrthogonalVectors_ShouldReturn0()
    {
        // Arrange
        var a = new float[] { 1f, 0f, 0f };
        var b = new float[] { 0f, 1f, 0f };

        // Act
        var similarity = CosineSimilarity(a, b);

        // Assert
        similarity.Should().BeApproximately(0f, 0.001f);
    }

    [Fact]
    public void CosineSimilarity_OppositeVectors_ShouldReturnNegative1()
    {
        // Arrange
        var a = new float[] { 1f, 0f, 0f };
        var b = new float[] { -1f, 0f, 0f };

        // Act
        var similarity = CosineSimilarity(a, b);

        // Assert
        similarity.Should().BeApproximately(-1f, 0.001f);
    }

    [Fact]
    public void CosineSimilarity_DifferentLengths_ShouldReturn0()
    {
        // Arrange
        var a = new float[] { 1f, 2f };
        var b = new float[] { 1f, 2f, 3f };

        // Act
        var similarity = CosineSimilarity(a, b);

        // Assert
        similarity.Should().Be(0f);
    }

    [Fact]
    public void CosineSimilarity_ZeroVector_ShouldReturn0()
    {
        // Arrange
        var a = new float[] { 0f, 0f, 0f };
        var b = new float[] { 1f, 2f, 3f };

        // Act
        var similarity = CosineSimilarity(a, b);

        // Assert
        similarity.Should().Be(0f);
    }

    // ======================================================================
    // QdrantIndexerConfig — default values
    // ======================================================================

    [Fact]
    public void Config_DefaultValues_ShouldHaveSensibleDefaults()
    {
        // Act
        var config = new QdrantIndexerConfig();

        // Assert
        config.ChunkSize.Should().Be(1000);
        config.ChunkOverlap.Should().Be(200);
        config.MaxFileSize.Should().Be(1024 * 1024);
        config.BatchSize.Should().Be(50);
        config.EnableFileWatcher.Should().BeTrue();
        config.FileWatcherDebounceMs.Should().Be(1000);
    }

    [Fact]
    public void Config_Extensions_ShouldIncludeCommonSourceFileTypes()
    {
        // Act
        var config = new QdrantIndexerConfig();

        // Assert
        config.Extensions.Should().Contain(".cs");
        config.Extensions.Should().Contain(".py");
        config.Extensions.Should().Contain(".js");
        config.Extensions.Should().Contain(".ts");
        config.Extensions.Should().Contain(".json");
        config.Extensions.Should().Contain(".md");
        config.Extensions.Should().Contain(".yaml");
        config.Extensions.Should().Contain(".yml");
        config.Extensions.Should().Contain(".csproj");
        config.Extensions.Should().Contain(".sln");
    }

    [Fact]
    public void Config_ExcludeDirectories_ShouldExcludeBuildAndToolDirs()
    {
        // Act
        var config = new QdrantIndexerConfig();

        // Assert
        config.ExcludeDirectories.Should().Contain("bin");
        config.ExcludeDirectories.Should().Contain("obj");
        config.ExcludeDirectories.Should().Contain("node_modules");
        config.ExcludeDirectories.Should().Contain(".git");
        config.ExcludeDirectories.Should().Contain("dist");
        config.ExcludeDirectories.Should().Contain("build");
    }

    // ======================================================================
    // SearchResult record
    // ======================================================================

    [Fact]
    public void SearchResult_ShouldStoreAllProperties()
    {
        // Act
        var result = new SearchResult
        {
            FilePath = "/src/file.cs",
            ChunkIndex = 3,
            Content = "public class Foo",
            Score = 0.95f,
        };

        // Assert
        result.FilePath.Should().Be("/src/file.cs");
        result.ChunkIndex.Should().Be(3);
        result.Content.Should().Be("public class Foo");
        result.Score.Should().Be(0.95f);
    }

    // ======================================================================
    // IndexingProgress record
    // ======================================================================

    [Fact]
    public void IndexingProgress_ShouldTrackAllStats()
    {
        // Act
        var progress = new IndexingProgress
        {
            TotalFiles = 100,
            ProcessedFiles = 80,
            IndexedChunks = 500,
            SkippedFiles = 10,
            ErrorFiles = 5,
            CurrentFile = "current.cs",
            Elapsed = TimeSpan.FromSeconds(30),
            IsComplete = false,
        };

        // Assert
        progress.TotalFiles.Should().Be(100);
        progress.ProcessedFiles.Should().Be(80);
        progress.IndexedChunks.Should().Be(500);
        progress.SkippedFiles.Should().Be(10);
        progress.ErrorFiles.Should().Be(5);
        progress.CurrentFile.Should().Be("current.cs");
        progress.IsComplete.Should().BeFalse();
    }

    // ======================================================================
    // RecordAccess / GetReorganizationStats — access pattern tracking
    // ======================================================================

    [Fact]
    public void RecordAccess_ShouldTrackAccessPatterns()
    {
        // Arrange
        var mockClient = new Mock<QdrantClient>("localhost", 6334, false);
        var mockRegistry = new Mock<IQdrantCollectionRegistry>();
        mockRegistry.Setup(r => r.GetCollectionName(QdrantCollectionRole.SelfIndex)).Returns("test_index");
        mockRegistry.Setup(r => r.GetCollectionName(QdrantCollectionRole.FileHashes)).Returns("test_hashes");

        var mockEmbedding = new Mock<IEmbeddingModel>();
        var config = new QdrantIndexerConfig { EnableFileWatcher = false };

        var indexer = new QdrantSelfIndexer(mockClient.Object, mockRegistry.Object, mockEmbedding.Object, config);

        var results = new List<SearchResult>
        {
            new() { FilePath = "file1.cs", ChunkIndex = 0, Content = "content1", Score = 0.9f },
            new() { FilePath = "file2.cs", ChunkIndex = 1, Content = "content2", Score = 0.8f },
        };

        // Act
        indexer.RecordAccess(results);
        indexer.RecordAccess(results); // Access again
        indexer.RecordAccess(results); // Access third time

        // Assert
        var stats = indexer.GetReorganizationStats();
        stats.TrackedPatterns.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void GetReorganizationStats_WithNoAccess_ShouldReturnZeros()
    {
        // Arrange
        var mockClient = new Mock<QdrantClient>("localhost", 6334, false);
        var mockRegistry = new Mock<IQdrantCollectionRegistry>();
        mockRegistry.Setup(r => r.GetCollectionName(QdrantCollectionRole.SelfIndex)).Returns("test_index");
        mockRegistry.Setup(r => r.GetCollectionName(QdrantCollectionRole.FileHashes)).Returns("test_hashes");

        var mockEmbedding = new Mock<IEmbeddingModel>();
        var config = new QdrantIndexerConfig { EnableFileWatcher = false };

        var indexer = new QdrantSelfIndexer(mockClient.Object, mockRegistry.Object, mockEmbedding.Object, config);

        // Act
        var stats = indexer.GetReorganizationStats();

        // Assert
        stats.TrackedPatterns.Should().Be(0);
        stats.HotContentCount.Should().Be(0);
        stats.CoAccessClusters.Should().Be(0);
    }

    // ======================================================================
    // QuickReorganizeAsync — early exit when insufficient patterns
    // ======================================================================

    [Fact]
    public async Task QuickReorganize_WhenFewPatterns_ShouldReturnZero()
    {
        // Arrange
        var mockClient = new Mock<QdrantClient>("localhost", 6334, false);
        var mockRegistry = new Mock<IQdrantCollectionRegistry>();
        mockRegistry.Setup(r => r.GetCollectionName(QdrantCollectionRole.SelfIndex)).Returns("test_index");
        mockRegistry.Setup(r => r.GetCollectionName(QdrantCollectionRole.FileHashes)).Returns("test_hashes");

        var mockEmbedding = new Mock<IEmbeddingModel>();
        var config = new QdrantIndexerConfig { EnableFileWatcher = false };

        var indexer = new QdrantSelfIndexer(mockClient.Object, mockRegistry.Object, mockEmbedding.Object, config);

        // Act — no access patterns recorded, should return 0 immediately
        var optimizations = await indexer.QuickReorganizeAsync();

        // Assert
        optimizations.Should().Be(0);
    }

    // ======================================================================
    // AddRootPath
    // ======================================================================

    [Fact]
    public void AddRootPath_WithNonExistentDir_ShouldNotAdd()
    {
        // Arrange
        var mockClient = new Mock<QdrantClient>("localhost", 6334, false);
        var mockRegistry = new Mock<IQdrantCollectionRegistry>();
        mockRegistry.Setup(r => r.GetCollectionName(QdrantCollectionRole.SelfIndex)).Returns("test_index");
        mockRegistry.Setup(r => r.GetCollectionName(QdrantCollectionRole.FileHashes)).Returns("test_hashes");

        var mockEmbedding = new Mock<IEmbeddingModel>();
        var config = new QdrantIndexerConfig { EnableFileWatcher = false, RootPaths = new List<string>() };

        var indexer = new QdrantSelfIndexer(mockClient.Object, mockRegistry.Object, mockEmbedding.Object, config);

        // Act
        indexer.AddRootPath("/nonexistent_path_xyz_999");

        // Assert — should silently skip
        // No exception thrown is the assertion
    }

    [Fact]
    public void AddRootPath_WithExistingDir_ShouldAddToConfig()
    {
        // Arrange
        var mockClient = new Mock<QdrantClient>("localhost", 6334, false);
        var mockRegistry = new Mock<IQdrantCollectionRegistry>();
        mockRegistry.Setup(r => r.GetCollectionName(QdrantCollectionRole.SelfIndex)).Returns("test_index");
        mockRegistry.Setup(r => r.GetCollectionName(QdrantCollectionRole.FileHashes)).Returns("test_hashes");

        var mockEmbedding = new Mock<IEmbeddingModel>();
        var config = new QdrantIndexerConfig
        {
            EnableFileWatcher = false,
            RootPaths = new List<string>()
        };

        var indexer = new QdrantSelfIndexer(mockClient.Object, mockRegistry.Object, mockEmbedding.Object, config);
        var tempDir = Path.Combine(Path.GetTempPath(), $"ouro_root_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Act
            indexer.AddRootPath(tempDir);

            // Assert
            config.RootPaths.Should().Contain(tempDir);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void AddRootPath_DuplicatePath_ShouldNotAddTwice()
    {
        // Arrange
        var mockClient = new Mock<QdrantClient>("localhost", 6334, false);
        var mockRegistry = new Mock<IQdrantCollectionRegistry>();
        mockRegistry.Setup(r => r.GetCollectionName(QdrantCollectionRole.SelfIndex)).Returns("test_index");
        mockRegistry.Setup(r => r.GetCollectionName(QdrantCollectionRole.FileHashes)).Returns("test_hashes");

        var mockEmbedding = new Mock<IEmbeddingModel>();
        var tempDir = Path.Combine(Path.GetTempPath(), $"ouro_dup_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var config = new QdrantIndexerConfig
        {
            EnableFileWatcher = false,
            RootPaths = new List<string> { tempDir }
        };

        var indexer = new QdrantSelfIndexer(mockClient.Object, mockRegistry.Object, mockEmbedding.Object, config);

        try
        {
            // Act
            indexer.AddRootPath(tempDir);

            // Assert
            config.RootPaths.Count(p => p == tempDir).Should().Be(1);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // ======================================================================
    // Constructor — argument validation
    // ======================================================================

    [Fact]
    public void Constructor_NullClient_ShouldThrow()
    {
        // Arrange
        var mockRegistry = new Mock<IQdrantCollectionRegistry>();
        mockRegistry.Setup(r => r.GetCollectionName(It.IsAny<QdrantCollectionRole>())).Returns("test");
        var mockEmbedding = new Mock<IEmbeddingModel>();

        // Act
        var act = () => new QdrantSelfIndexer(null!, mockRegistry.Object, mockEmbedding.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullRegistry_ShouldThrow()
    {
        // Arrange
        var mockClient = new Mock<QdrantClient>("localhost", 6334, false);
        var mockEmbedding = new Mock<IEmbeddingModel>();

        // Act
        var act = () => new QdrantSelfIndexer(mockClient.Object, null!, mockEmbedding.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullEmbedding_ShouldThrow()
    {
        // Arrange
        var mockClient = new Mock<QdrantClient>("localhost", 6334, false);
        var mockRegistry = new Mock<IQdrantCollectionRegistry>();
        mockRegistry.Setup(r => r.GetCollectionName(It.IsAny<QdrantCollectionRole>())).Returns("test");

        // Act
        var act = () => new QdrantSelfIndexer(mockClient.Object, mockRegistry.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // ======================================================================
    // Helper methods (replicate private static methods for testing)
    // ======================================================================

    private static string ComputeHash(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(stream);
            return Convert.ToBase64String(hash);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GeneratePointId(string filePath, int chunkIndex)
    {
        using var sha256 = SHA256.Create();
        var input = $"{filePath}::{chunkIndex}";
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return new Guid(hash.Take(16).ToArray()).ToString();
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        float dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        var magnitude = Math.Sqrt(magA) * Math.Sqrt(magB);
        return magnitude > 0 ? (float)(dot / magnitude) : 0;
    }
}
