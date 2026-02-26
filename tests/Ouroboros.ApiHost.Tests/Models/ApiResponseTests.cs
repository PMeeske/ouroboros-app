using Ouroboros.ApiHost.Models;

namespace Ouroboros.Tests.Models;

[Trait("Category", "Unit")]
public sealed class ApiResponseTests
{
    [Fact]
    public void Ok_WithData_ReturnsSuccessResponse()
    {
        // Arrange
        var data = "test result";

        // Act
        var response = ApiResponse<string>.Ok(data);

        // Assert
        response.Success.Should().BeTrue();
        response.Data.Should().Be("test result");
        response.Error.Should().BeNull();
        response.ExecutionTimeMs.Should().BeNull();
    }

    [Fact]
    public void Ok_WithDataAndExecutionTime_ReturnsSuccessResponseWithTiming()
    {
        // Arrange & Act
        var response = ApiResponse<int>.Ok(42, 150);

        // Assert
        response.Success.Should().BeTrue();
        response.Data.Should().Be(42);
        response.ExecutionTimeMs.Should().Be(150);
        response.Error.Should().BeNull();
    }

    [Fact]
    public void Fail_WithErrorMessage_ReturnsFailureResponse()
    {
        // Arrange & Act
        var response = ApiResponse<string>.Fail("Something went wrong");

        // Assert
        response.Success.Should().BeFalse();
        response.Error.Should().Be("Something went wrong");
        response.Data.Should().BeNull();
        response.ExecutionTimeMs.Should().BeNull();
    }

    [Fact]
    public void Ok_WithComplexType_ReturnsSuccessResponse()
    {
        // Arrange
        var data = new AskResponse { Answer = "42", Model = "llama3" };

        // Act
        var response = ApiResponse<AskResponse>.Ok(data, 200);

        // Assert
        response.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Answer.Should().Be("42");
        response.Data.Model.Should().Be("llama3");
    }

    [Fact]
    public void Fail_WithGenericType_HasNullData()
    {
        // Arrange & Act
        var response = ApiResponse<List<string>>.Fail("No items found");

        // Assert
        response.Success.Should().BeFalse();
        response.Data.Should().BeNull();
    }
}
