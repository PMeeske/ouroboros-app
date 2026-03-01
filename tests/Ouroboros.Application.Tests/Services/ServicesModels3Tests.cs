using FluentAssertions;
using Ouroboros.Application.Services;
using Xunit;

namespace Ouroboros.Tests.Services;

[Trait("Category", "Unit")]
public class ServicesModels3Tests
{
    // --- ConversationMemoryConfig ---

    [Fact]
    public void ConversationMemoryConfig_ShouldHaveDefaults()
    {
        var config = new ConversationMemoryConfig();

        config.StorageDirectory.Should().NotBeEmpty();
        config.CollectionName.Should().Be("ouroboros_conversations");
        config.MaxActiveTurns.Should().Be(50);
        config.RecentSessionsToLoad.Should().Be(5);
    }

    // --- QdrantIndexerConfig ---

    [Fact]
    public void QdrantIndexerConfig_ShouldHaveDefaults()
    {
        var config = new QdrantIndexerConfig();

        config.CollectionName.Should().Be("ouroboros_selfindex");
        config.HashCollectionName.Should().Be("ouroboros_filehashes");
        config.RootPaths.Should().BeEmpty();
        config.Extensions.Should().Contain(".cs");
        config.Extensions.Should().Contain(".py");
        config.ExcludeDirectories.Should().Contain("bin");
        config.ExcludeDirectories.Should().Contain("node_modules");
        config.ChunkSize.Should().Be(1000);
        config.ChunkOverlap.Should().Be(200);
        config.MaxFileSize.Should().Be(1024 * 1024);
        config.BatchSize.Should().Be(50);
        config.EnableFileWatcher.Should().BeTrue();
        config.FileWatcherDebounceMs.Should().Be(1000);
    }

    // --- SelfIndexWarmupStats ---

    [Fact]
    public void SelfIndexWarmupStats_ShouldHaveDefaults()
    {
        var stats = new SelfIndexWarmupStats();

        stats.TotalVectors.Should().Be(0);
        stats.IndexedFiles.Should().Be(0);
        stats.CollectionName.Should().BeNull();
        stats.SearchQueriesTested.Should().Be(0);
        stats.SearchQueriesSucceeded.Should().Be(0);
    }

    // --- ReorganizationStats ---

    [Fact]
    public void ReorganizationStats_ShouldSetProperties()
    {
        var stats = new ReorganizationStats
        {
            TrackedPatterns = 5,
            HotContentCount = 3,
            CoAccessClusters = 2,
            TopAccessedFiles = new List<(string, int)> { ("/src/test.cs", 10) }
        };

        stats.TrackedPatterns.Should().Be(5);
        stats.HotContentCount.Should().Be(3);
        stats.CoAccessClusters.Should().Be(2);
        stats.TopAccessedFiles.Should().HaveCount(1);
    }

    // --- ReorganizationResult ---

    [Fact]
    public void ReorganizationResult_ShouldSetProperties()
    {
        var result = new ReorganizationResult
        {
            ClustersFound = 3,
            ConsolidatedChunks = 10,
            DuplicatesRemoved = 2,
            SummariesCreated = 3,
            Duration = TimeSpan.FromSeconds(5),
            Insights = new List<string> { "Found cluster A" }
        };

        result.ClustersFound.Should().Be(3);
        result.ConsolidatedChunks.Should().Be(10);
        result.DuplicatesRemoved.Should().Be(2);
        result.Insights.Should().HaveCount(1);
    }

    // --- PersistenceStats ---

    [Fact]
    public void PersistenceStats_ShouldHaveDefaults()
    {
        var stats = new PersistenceStats();

        stats.IsConnected.Should().BeFalse();
        stats.CollectionName.Should().BeEmpty();
        stats.TotalPoints.Should().Be(0);
        stats.FileBackups.Should().Be(0);
    }

    // --- StreamDetail ---

    [Fact]
    public void StreamDetail_ShouldSetProperties()
    {
        var detail = new StreamDetail
        {
            StreamId = "stream-1",
            AtomCount = 50,
            LastActivity = DateTime.UtcNow
        };

        detail.StreamId.Should().Be("stream-1");
        detail.AtomCount.Should().Be(50);
    }

    // --- ParallelStreamStats ---

    [Fact]
    public void ParallelStreamStats_ShouldSetProperties()
    {
        var stats = new ParallelStreamStats
        {
            ActiveStreams = 3,
            TotalAtomsGenerated = 150,
            ConvergenceEvents = 2,
            StreamDetails = new List<StreamDetail>
            {
                new StreamDetail { StreamId = "s1", AtomCount = 50 }
            }
        };

        stats.ActiveStreams.Should().Be(3);
        stats.TotalAtomsGenerated.Should().Be(150);
        stats.ConvergenceEvents.Should().Be(2);
        stats.StreamDetails.Should().HaveCount(1);
    }

    // --- ModuloSquareSolution ---

    [Fact]
    public void ModuloSquareSolution_ShouldSetProperties()
    {
        var solution = new ModuloSquareSolution
        {
            Target = 17,
            SquareRoot = 4,
            Derivation = "4^2 = 16 mod 17",
            IsVerified = true
        };

        solution.Target.Should().Be(17);
        solution.SquareRoot.Should().Be(4);
        solution.Derivation.Should().NotBeEmpty();
        solution.IsVerified.Should().BeTrue();
    }

    // --- DetectionEvent ---

    [Fact]
    public void DetectionEvent_ShouldSetProperties()
    {
        var now = DateTime.UtcNow;
        var evt = new DetectionEvent("motion-detector", "motion", 0.85, now);

        evt.ModuleName.Should().Be("motion-detector");
        evt.EventType.Should().Be("motion");
        evt.Confidence.Should().Be(0.85);
        evt.Timestamp.Should().Be(now);
        evt.Payload.Should().BeNull();
    }
}
