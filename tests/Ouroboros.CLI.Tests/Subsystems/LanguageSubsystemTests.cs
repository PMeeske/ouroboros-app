using Ouroboros.CLI.Services;
using Ouroboros.CLI.Subsystems;

namespace Ouroboros.Tests.CLI.Subsystems;

[Trait("Category", "Unit")]
public class LanguageSubsystemTests
{
    [Fact]
    public void Name_IsLanguage()
    {
        var subsystem = new LanguageSubsystem();

        subsystem.Name.Should().Be("Language");
    }

    [Fact]
    public void IsInitialized_InitiallyFalse()
    {
        var subsystem = new LanguageSubsystem();

        subsystem.IsInitialized.Should().BeFalse();
    }

    [Fact]
    public void DefaultModel_IsAyaExpanse()
    {
        LanguageSubsystem.DefaultModel.Should().Be("aya-expanse:8b");
    }

    [Fact]
    public void ImplementsILanguageSubsystem()
    {
        var subsystem = new LanguageSubsystem();
        subsystem.Should().BeAssignableTo<ILanguageSubsystem>();
    }

    [Fact]
    public void ImplementsIAgentSubsystem()
    {
        var subsystem = new LanguageSubsystem();
        subsystem.Should().BeAssignableTo<IAgentSubsystem>();
    }

    [Fact]
    public async Task DetectStaticAsync_WithNoInstance_FallsBackToHeuristic()
    {
        // When no LanguageSubsystem is initialized, should fall back to heuristic
        var result = await LanguageSubsystem.DetectStaticAsync("Hallo Welt");

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task DisposeAsync_DoesNotThrow()
    {
        var subsystem = new LanguageSubsystem();

        var action = async () => await subsystem.DisposeAsync();

        await action.Should().NotThrowAsync();
    }
}
