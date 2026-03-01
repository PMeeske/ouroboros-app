using FluentAssertions;
using Ouroboros.Application.Learning;
using Ouroboros.Core.LawsOfForm;
using Xunit;

namespace Ouroboros.Tests.Learning;

[Trait("Category", "Unit")]
public class DreamEmbeddingTests
{
    private static DreamEmbedding CreateSample(float[] composite)
    {
        var stages = new Dictionary<DreamStage, float[]>
        {
            [DreamStage.Void] = new float[] { 1.0f, 0.0f, 0.0f },
            [DreamStage.Distinction] = new float[] { 0.0f, 1.0f, 0.0f }
        };

        return new DreamEmbedding("test-circumstance", stages, composite, DateTime.UtcNow);
    }

    [Fact]
    public void Constructor_ShouldSetProperties()
    {
        var composite = new float[] { 0.5f, 0.5f, 0.5f };
        var embedding = CreateSample(composite);

        embedding.Circumstance.Should().Be("test-circumstance");
        embedding.StageEmbeddings.Should().HaveCount(2);
        embedding.CompositeEmbedding.Should().BeEquivalentTo(composite);
    }

    [Fact]
    public void GetStageEmbedding_ExistingStage_ShouldReturnEmbedding()
    {
        var embedding = CreateSample(new float[] { 0.5f, 0.5f, 0.5f });

        var result = embedding.GetStageEmbedding(DreamStage.Void);

        result.Should().NotBeNull();
        result![0].Should().Be(1.0f);
    }

    [Fact]
    public void GetStageEmbedding_MissingStage_ShouldReturnNull()
    {
        var embedding = CreateSample(new float[] { 0.5f, 0.5f, 0.5f });

        var result = embedding.GetStageEmbedding(DreamStage.Recognition);

        result.Should().BeNull();
    }

    [Fact]
    public void ComputeSimilarity_IdenticalVectors_ShouldBeOne()
    {
        var vec = new float[] { 1.0f, 0.0f, 0.0f };
        var a = CreateSample(vec);
        var b = CreateSample(vec);

        var similarity = a.ComputeSimilarity(b);

        similarity.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void ComputeSimilarity_OrthogonalVectors_ShouldBeZero()
    {
        var a = CreateSample(new float[] { 1.0f, 0.0f, 0.0f });
        var b = CreateSample(new float[] { 0.0f, 1.0f, 0.0f });

        var similarity = a.ComputeSimilarity(b);

        similarity.Should().BeApproximately(0.0, 0.001);
    }

    [Fact]
    public void ComputeSimilarity_DifferentLengths_ShouldReturnZero()
    {
        var a = CreateSample(new float[] { 1.0f, 0.0f });
        var stages = new Dictionary<DreamStage, float[]>();
        var b = new DreamEmbedding("b", stages, new float[] { 1.0f, 0.0f, 0.0f }, DateTime.UtcNow);

        var similarity = a.ComputeSimilarity(b);

        similarity.Should().Be(0.0);
    }

    [Fact]
    public void ComputeSimilarity_ZeroVector_ShouldReturnZero()
    {
        var a = CreateSample(new float[] { 0.0f, 0.0f, 0.0f });
        var b = CreateSample(new float[] { 1.0f, 0.0f, 0.0f });

        var similarity = a.ComputeSimilarity(b);

        similarity.Should().Be(0.0);
    }

    [Fact]
    public void ComputeSimilarity_NullOther_ShouldThrow()
    {
        var a = CreateSample(new float[] { 1.0f });

        var act = () => a.ComputeSimilarity(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
