using Ouroboros.CLI.Commands;

namespace Ouroboros.Tests.CLI.Commands.MeTTa;

[Trait("Category", "Unit")]
public class MeTTaInteractiveModeTests
{
    [Fact]
    public void MeTTaInteractiveMode_Exists()
    {
        typeof(MeTTaInteractiveMode).Should().NotBeNull();
    }

    [Fact]
    public void MeTTaInteractiveMode_IsStaticClass()
    {
        typeof(MeTTaInteractiveMode).IsAbstract.Should().BeTrue();
        typeof(MeTTaInteractiveMode).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void MeTTaConfig_IsRecord()
    {
        typeof(MeTTaConfig).GetMethod("<Clone>$").Should().NotBeNull();
    }

    [Fact]
    public void MeTTaConfig_DefaultValues()
    {
        var config = new MeTTaConfig();
        config.Voice.Should().BeFalse();
        config.Debug.Should().BeFalse();
    }
}
