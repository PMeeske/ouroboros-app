using Ouroboros.Options;

namespace Ouroboros.Tests.CLI.Options;

[Trait("Category", "Unit")]
public class SetupOptionsTests
{
    [Fact]
    public void DefaultValues_AreAllFalse()
    {
        var options = new SetupOptions();

        options.InstallOllama.Should().BeFalse();
        options.ConfigureAuth.Should().BeFalse();
        options.InstallMeTTa.Should().BeFalse();
        options.InstallVectorStore.Should().BeFalse();
        options.All.Should().BeFalse();
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        var options = new SetupOptions
        {
            InstallOllama = true,
            ConfigureAuth = true,
            InstallMeTTa = true,
            InstallVectorStore = true,
            All = true
        };

        options.InstallOllama.Should().BeTrue();
        options.ConfigureAuth.Should().BeTrue();
        options.InstallMeTTa.Should().BeTrue();
        options.InstallVectorStore.Should().BeTrue();
        options.All.Should().BeTrue();
    }
}
