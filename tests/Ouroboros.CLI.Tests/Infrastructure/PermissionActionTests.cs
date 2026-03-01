using Ouroboros.CLI.Infrastructure;

namespace Ouroboros.Tests.CLI.Infrastructure;

[Trait("Category", "Unit")]
public class PermissionActionTests
{
    [Fact]
    public void PermissionAction_HasExpectedValues()
    {
        Enum.IsDefined(typeof(PermissionAction), PermissionAction.Allow).Should().BeTrue();
        Enum.IsDefined(typeof(PermissionAction), PermissionAction.Deny).Should().BeTrue();
    }

    [Fact]
    public void PermissionAction_HasExactlyTwoValues()
    {
        var values = Enum.GetValues<PermissionAction>();
        values.Should().HaveCount(2);
    }
}
