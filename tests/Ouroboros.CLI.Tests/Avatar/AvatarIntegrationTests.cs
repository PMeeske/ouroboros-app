using Ouroboros.CLI.Avatar;

namespace Ouroboros.Tests.CLI.Avatar;

[Trait("Category", "Unit")]
public class AvatarIntegrationTests
{
    [Fact]
    public void AvatarIntegration_IsStaticClass()
    {
        typeof(AvatarIntegration).IsAbstract.Should().BeTrue();
        typeof(AvatarIntegration).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void CreateAndStartAsync_MethodExists()
    {
        var method = typeof(AvatarIntegration).GetMethod("CreateAndStartAsync");
        method.Should().NotBeNull();
    }

    [Fact]
    public void CreateAndStartWithVisionAsync_MethodExists()
    {
        var method = typeof(AvatarIntegration).GetMethod("CreateAndStartWithVisionAsync");
        method.Should().NotBeNull();
    }

    [Fact]
    public void BindToPresenceStream_MethodExists()
    {
        var method = typeof(AvatarIntegration).GetMethod("BindToPresenceStream");
        method.Should().NotBeNull();
    }

    [Fact]
    public void PushState_MethodExists()
    {
        var method = typeof(AvatarIntegration).GetMethod("PushState");
        method.Should().NotBeNull();
    }
}
