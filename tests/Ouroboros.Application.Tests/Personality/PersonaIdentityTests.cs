using FluentAssertions;
using Ouroboros.Application.Personality;
using Xunit;

namespace Ouroboros.Tests.Personality;

[Trait("Category", "Unit")]
public class PersonaIdentityTests
{
    [Fact]
    public void Create_ShouldSetNameAndId()
    {
        var identity = PersonaIdentity.Create("Iaret", "iaret-001");

        identity.Name.Should().Be("Iaret");
        identity.PersonaId.Should().Be("iaret-001");
    }

    [Fact]
    public void Create_ShouldHaveTraits()
    {
        var identity = PersonaIdentity.Create("Iaret", "iaret-001");

        identity.Traits.Should().NotBeEmpty();
        identity.Traits.Should().Contain("commanding");
        identity.Traits.Should().Contain("warm");
    }

    [Fact]
    public void Create_ShouldHaveValues()
    {
        var identity = PersonaIdentity.Create("Iaret", "iaret-001");

        identity.Values.Should().NotBeEmpty();
        identity.Values.Should().Contain("sovereignty");
    }

    [Fact]
    public void Create_ShouldHaveCoreIdentity()
    {
        var identity = PersonaIdentity.Create("Iaret", "iaret-001");

        identity.CoreIdentity.Should().NotBeNullOrWhiteSpace();
        identity.CoreIdentity.Should().Contain("Iaret");
    }

    [Fact]
    public void Defaults_Generation_ShouldBe1()
    {
        var identity = PersonaIdentity.Create("Iaret", "iaret-001");

        identity.Generation.Should().Be(1);
        identity.ParentPersonaId.Should().BeNull();
    }
}
