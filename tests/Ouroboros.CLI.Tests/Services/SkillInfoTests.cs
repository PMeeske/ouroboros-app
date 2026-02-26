using Ouroboros.CLI.Services;

namespace Ouroboros.Tests.CLI.Services;

[Trait("Category", "Unit")]
public class SkillInfoTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var skill = new SkillInfo();

        skill.Name.Should().BeEmpty();
        skill.Description.Should().BeEmpty();
        skill.SuccessRate.Should().Be(0f);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var skill = new SkillInfo
        {
            Name = "code_search",
            Description = "Search through codebase",
            SuccessRate = 0.95f
        };

        skill.Name.Should().Be("code_search");
        skill.Description.Should().Be("Search through codebase");
        skill.SuccessRate.Should().Be(0.95f);
    }

    [Fact]
    public void SuccessRate_AcceptsBoundaryValues()
    {
        var skill = new SkillInfo { SuccessRate = 0.0f };
        skill.SuccessRate.Should().Be(0.0f);

        skill.SuccessRate = 1.0f;
        skill.SuccessRate.Should().Be(1.0f);
    }
}
