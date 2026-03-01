using FluentAssertions;
using Ouroboros.Application.Configuration;
using Xunit;

namespace Ouroboros.Tests.Configuration;

[Trait("Category", "Unit")]
public class RagConfigTests
{
    [Fact]
    public void DecomposeAndAggregateRagConfig_Defaults_ShouldHaveExpectedValues()
    {
        var config = new DecomposeAndAggregateRagConfig();

        config.SubQuestions.Should().Be(4);
        config.DocsPerSubQuestion.Should().Be(6);
        config.InitialRetrievalCount.Should().Be(24);
        config.Separator.Should().Contain("---");
        config.StreamOutputs.Should().BeFalse();
        config.DecomposeTemplate.Should().BeNull();
        config.SubQuestionTemplate.Should().BeNull();
        config.FinalTemplate.Should().BeNull();
    }

    [Fact]
    public void DivideAndConquerRagConfig_Defaults_ShouldHaveExpectedValues()
    {
        var config = new DivideAndConquerRagConfig();

        config.RetrievalCount.Should().Be(24);
        config.GroupSize.Should().Be(6);
        config.Separator.Should().Contain("---");
        config.CustomTemplate.Should().BeNull();
        config.FinalTemplate.Should().BeNull();
        config.StreamPartials.Should().BeFalse();
    }

    [Fact]
    public void MarkdownEnhancementConfig_ShouldHaveDefaults()
    {
        var config = new MarkdownEnhancementConfig { FilePath = "/test.md" };

        config.FilePath.Should().Be("/test.md");
        config.Iterations.Should().Be(1);
        config.ContextCount.Should().Be(8);
        config.CreateBackup.Should().BeTrue();
        config.Goal.Should().BeNull();
    }

    [Fact]
    public void DirectoryIngestionConfig_Defaults_ShouldHaveExpectedValues()
    {
        var config = new DirectoryIngestionConfig { Root = "/test" };

        config.Root.Should().Be("/test");
        config.Recursive.Should().BeTrue();
        config.Extensions.Should().BeEmpty();
        config.ExcludeDirectories.Should().BeEmpty();
        config.MaxFileBytes.Should().Be(0);
        config.ChunkSize.Should().Be(DefaultIngestionSettings.ChunkSize);
        config.ChunkOverlap.Should().Be(DefaultIngestionSettings.ChunkOverlap);
        config.BatchSize.Should().Be(0);
    }

    [Fact]
    public void ZipIngestionConfig_Defaults_ShouldHaveExpectedValues()
    {
        var config = new ZipIngestionConfig { ArchivePath = "/test.zip" };

        config.IncludeXmlText.Should().BeTrue();
        config.CsvMaxLines.Should().Be(DefaultIngestionSettings.CsvMaxLines);
        config.BinaryMaxBytes.Should().Be(DefaultIngestionSettings.BinaryMaxBytes);
        config.MaxTotalBytes.Should().Be(DefaultIngestionSettings.MaxArchiveSizeBytes);
        config.MaxCompressionRatio.Should().Be(DefaultIngestionSettings.MaxCompressionRatio);
        config.NoEmbed.Should().BeFalse();
        config.BatchSize.Should().Be(DefaultIngestionSettings.DefaultBatchSize);
    }
}
