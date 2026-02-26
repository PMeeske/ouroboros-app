using Ouroboros.ApiHost.Client;

namespace Ouroboros.Tests.Client;

[Trait("Category", "Unit")]
public sealed class OuroborosApiClientConstantsTests
{
    [Fact]
    public void HttpClientName_HasExpectedValue()
    {
        // Assert
        OuroborosApiClientConstants.HttpClientName.Should().Be("OuroborosApi");
    }

    [Fact]
    public void HttpClientName_IsNotEmpty()
    {
        // Assert
        OuroborosApiClientConstants.HttpClientName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void HttpClientName_IsConstant_SameReference()
    {
        // Arrange
        var value1 = OuroborosApiClientConstants.HttpClientName;
        var value2 = OuroborosApiClientConstants.HttpClientName;

        // Assert
        ReferenceEquals(value1, value2).Should().BeTrue();
    }
}
