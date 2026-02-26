using Ouroboros.CLI.Commands;

namespace Ouroboros.Tests.CLI.Commands.Models;

[Trait("Category", "Unit")]
public class PersonaDefinitionTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var persona = new PersonaDefinition(
            Name: "Iaret",
            Voice: "en-US-AvaMultilingualNeural",
            Traits: new[] { "curious", "honest" },
            Moods: new[] { "calm", "excited" },
            CoreIdentity: "Egyptian goddess of knowledge");

        persona.Name.Should().Be("Iaret");
        persona.Voice.Should().Be("en-US-AvaMultilingualNeural");
        persona.Traits.Should().HaveCount(2);
        persona.Traits.Should().Contain("curious");
        persona.Moods.Should().HaveCount(2);
        persona.CoreIdentity.Should().Be("Egyptian goddess of knowledge");
    }

    [Fact]
    public void Equality_TwoIdentical_AreEqual()
    {
        var traits = new[] { "trait1" };
        var moods = new[] { "mood1" };

        var p1 = new PersonaDefinition("Name", "Voice", traits, moods, "Identity");
        var p2 = new PersonaDefinition("Name", "Voice", traits, moods, "Identity");

        p1.Should().Be(p2);
    }

    [Fact]
    public void With_CreatesModifiedCopy()
    {
        var persona = new PersonaDefinition("Iaret", "voice", new[] { "a" }, new[] { "b" }, "core");
        var modified = persona with { Name = "Phoenix" };

        modified.Name.Should().Be("Phoenix");
        modified.Voice.Should().Be("voice");
        persona.Name.Should().Be("Iaret");
    }
}
