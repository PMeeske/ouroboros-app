using FluentAssertions;
using Ouroboros.Application.Configuration;
using Xunit;

namespace Ouroboros.Tests.Configuration;

[Trait("Category", "Unit")]
public class DefaultIngestionSettingsTests
{
    [Fact]
    public void ChunkSize_ShouldBe1800()
    {
        DefaultIngestionSettings.ChunkSize.Should().Be(1800);
    }

    [Fact]
    public void ChunkOverlap_ShouldBe180()
    {
        DefaultIngestionSettings.ChunkOverlap.Should().Be(180);
    }

    [Fact]
    public void MaxArchiveSizeBytes_ShouldBe500MB()
    {
        DefaultIngestionSettings.MaxArchiveSizeBytes.Should().Be(500L * 1024 * 1024);
    }

    [Fact]
    public void MaxCompressionRatio_ShouldBe200()
    {
        DefaultIngestionSettings.MaxCompressionRatio.Should().Be(200.0);
    }

    [Fact]
    public void DefaultBatchSize_ShouldBe16()
    {
        DefaultIngestionSettings.DefaultBatchSize.Should().Be(16);
    }

    [Fact]
    public void DocumentSeparator_ShouldContainDashes()
    {
        DefaultIngestionSettings.DocumentSeparator.Should().Contain("---");
    }

    [Fact]
    public void CsvMaxLines_ShouldBe50()
    {
        DefaultIngestionSettings.CsvMaxLines.Should().Be(50);
    }

    [Fact]
    public void BinaryMaxBytes_ShouldBe128KB()
    {
        DefaultIngestionSettings.BinaryMaxBytes.Should().Be(128 * 1024);
    }
}
