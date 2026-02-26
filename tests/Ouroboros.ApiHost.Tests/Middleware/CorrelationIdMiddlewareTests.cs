using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Ouroboros.ApiHost.Middleware;

namespace Ouroboros.Tests.Middleware;

[Trait("Category", "Unit")]
public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_NoCorrelationIdHeader_GeneratesNewId()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var logger = new Mock<ILogger<CorrelationIdMiddleware>>();
        string? capturedCorrelationId = null;

        RequestDelegate next = ctx =>
        {
            capturedCorrelationId = ctx.Items["CorrelationId"]?.ToString();
            return Task.CompletedTask;
        };

        var middleware = new CorrelationIdMiddleware(next, logger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedCorrelationId.Should().NotBeNullOrEmpty();
        capturedCorrelationId!.Length.Should().Be(32); // GUID without dashes
    }

    [Fact]
    public async Task InvokeAsync_WithExistingCorrelationId_ReusesProvidedId()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-ID"] = "my-custom-id";
        var logger = new Mock<ILogger<CorrelationIdMiddleware>>();
        string? capturedCorrelationId = null;

        RequestDelegate next = ctx =>
        {
            capturedCorrelationId = ctx.Items["CorrelationId"]?.ToString();
            return Task.CompletedTask;
        };

        var middleware = new CorrelationIdMiddleware(next, logger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedCorrelationId.Should().Be("my-custom-id");
    }

    [Fact]
    public async Task InvokeAsync_StoresCorrelationIdInContextItems()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-ID"] = "response-test-id";
        var logger = new Mock<ILogger<CorrelationIdMiddleware>>();

        RequestDelegate next = _ => Task.CompletedTask;

        var middleware = new CorrelationIdMiddleware(next, logger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert - verify correlation ID stored in Items for downstream use
        context.Items["CorrelationId"].Should().Be("response-test-id");
    }

    [Fact]
    public async Task InvokeAsync_CallsNextDelegate()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var logger = new Mock<ILogger<CorrelationIdMiddleware>>();
        bool nextCalled = false;

        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new CorrelationIdMiddleware(next, logger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }
}
