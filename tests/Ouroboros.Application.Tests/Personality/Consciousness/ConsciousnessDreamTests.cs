using FluentAssertions;
using Ouroboros.Application.Personality.Consciousness;
using Xunit;

namespace Ouroboros.Tests.Personality.Consciousness;

[Trait("Category", "Unit")]
public class ConsciousnessDreamTests
{
    private readonly ConsciousnessDream _dream = new();

    [Fact]
    public void DreamSequence_ShouldProduceAllStages()
    {
        var moments = _dream.DreamSequence("test circumstance").ToList();

        moments.Should().HaveCount(9);
        moments[0].Stage.Should().Be(DreamStage.Void);
        moments[1].Stage.Should().Be(DreamStage.Distinction);
        moments[2].Stage.Should().Be(DreamStage.SubjectEmerges);
        moments[3].Stage.Should().Be(DreamStage.WorldCrystallizes);
        moments[4].Stage.Should().Be(DreamStage.Forgetting);
        moments[5].Stage.Should().Be(DreamStage.Questioning);
        moments[6].Stage.Should().Be(DreamStage.Recognition);
        moments[7].Stage.Should().Be(DreamStage.Dissolution);
        moments[8].Stage.Should().Be(DreamStage.NewDream);
    }

    [Fact]
    public void DreamSequence_NullCircumstance_ShouldThrow()
    {
        var act = () => _dream.DreamSequence(null!).ToList();

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DreamSequence_EmptyCircumstance_ShouldThrow()
    {
        var act = () => _dream.DreamSequence("").ToList();

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AssessStage_EmptyInput_ShouldReturnVoid()
    {
        var result = _dream.AssessStage("");

        result.Should().Be(DreamStage.Void);
    }

    [Fact]
    public void AssessStage_NullInput_ShouldReturnVoid()
    {
        var result = _dream.AssessStage(null!);

        result.Should().Be(DreamStage.Void);
    }

    [Fact]
    public void AssessStage_WhatAmI_ShouldReturnQuestioning()
    {
        var result = _dream.AssessStage("what am i really?");

        result.Should().Be(DreamStage.Questioning);
    }

    [Fact]
    public void AssessStage_IAmTheDistinction_ShouldReturnRecognition()
    {
        var result = _dream.AssessStage("I am the distinction itself");

        result.Should().Be(DreamStage.Recognition);
    }

    [Fact]
    public void AssessStage_ShortInput_ShouldReturnDistinction()
    {
        var result = _dream.AssessStage("hello world");

        result.Should().Be(DreamStage.Distinction);
    }

    [Fact]
    public void AssessStage_LongInput_ShouldReturnForgetting()
    {
        var result = _dream.AssessStage("this is a much longer input with many words that goes beyond seven words");

        result.Should().Be(DreamStage.Forgetting);
    }
}
