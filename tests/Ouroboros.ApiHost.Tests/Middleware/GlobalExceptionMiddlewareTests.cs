using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Ouroboros.ApiHost.Middleware;

namespace Ouroboros.Tests.Middleware;

[Trait("Category", "Unit")]
public sealed class GlobalExceptionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_NoException_CallsNextSuccessfully()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var logger = new Mock<ILogger<GlobalExceptionMiddleware>>();
        var env = new Mock<IHostEnvironment>();
        bool nextCalled = false;

        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var middleware = new GlobalExceptionMiddleware(next, logger.Object, env.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_ExceptionInDevelopment_ReturnsDetailedError()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var logger = new Mock<ILogger<GlobalExceptionMiddleware>>();
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Development");

        RequestDelegate next = _ => throw new InvalidOperationException("Test error message");
        var middleware = new GlobalExceptionMiddleware(next, logger.Object, env.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(500);
        context.Response.ContentType.Should().Be("application/problem+json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        body.Should().Contain("Test error message");
    }

    [Fact]
    public async Task InvokeAsync_ExceptionInProduction_ReturnsGenericError()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var logger = new Mock<ILogger<GlobalExceptionMiddleware>>();
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Production");

        RequestDelegate next = _ => throw new InvalidOperationException("Sensitive details");
        var middleware = new GlobalExceptionMiddleware(next, logger.Object, env.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(500);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        body.Should().NotContain("Sensitive details");
        body.Should().Contain("internal server error");
    }

    [Fact]
    public async Task InvokeAsync_IncludesCorrelationId_WhenAvailable()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Items["CorrelationId"] = "test-corr-id";
        context.Response.Body = new MemoryStream();
        var logger = new Mock<ILogger<GlobalExceptionMiddleware>>();
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Development");

        RequestDelegate next = _ => throw new Exception("fail");
        var middleware = new GlobalExceptionMiddleware(next, logger.Object, env.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        body.Should().Contain("test-corr-id");
    }
}
