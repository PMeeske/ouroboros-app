using Ouroboros.CLI.Abstractions;
using Ouroboros.CLI.Commands;

namespace Ouroboros.Tests.CLI.Abstractions;

[Trait("Category", "Unit")]
public class AgentContextTests
{
    [Fact]
    public void Constructor_WithConfig_SetsConfigProperty()
    {
        var config = new OuroborosConfig();

        var context = new AgentContext(config);

        context.Config.Should().BeSameAs(config);
    }

    [Fact]
    public void Constructor_WithCustomConfig_PreservesConfigValues()
    {
        var config = new OuroborosConfig(
            Persona: "TestPersona",
            Model: "test-model",
            Debug: true);

        var context = new AgentContext(config);

        context.Config.Persona.Should().Be("TestPersona");
        context.Config.Model.Should().Be("test-model");
        context.Config.Debug.Should().BeTrue();
    }

    [Fact]
    public void Equality_TwoWithSameConfig_AreEqual()
    {
        var config = new OuroborosConfig();
        var ctx1 = new AgentContext(config);
        var ctx2 = new AgentContext(config);

        ctx1.Should().Be(ctx2);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var config1 = new OuroborosConfig(Persona: "A");
        var config2 = new OuroborosConfig(Persona: "B");

        var ctx = new AgentContext(config1);
        var modified = ctx with { Config = config2 };

        modified.Config.Persona.Should().Be("B");
        ctx.Config.Persona.Should().Be("A");
    }
}
