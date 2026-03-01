using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Ouroboros.ApiHost.Middleware;

namespace Ouroboros.Tests.Middleware;

[Trait("Category", "Unit")]
public sealed class ApiKeyAuthMiddlewareTests
{
    private static IConfiguration BuildConfig(string? apiKey)
    {
        var configData = new Dictionary<string, string?>();
        if (apiKey != null)
            configData["ApiKey"] = apiKey;

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    [Fact]
    public async Task InvokeAsync_NoApiKeyConfigured_BypassesAuth()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/ask";
        var logger = new Mock<ILogger<ApiKeyAuthMiddleware>>();
        bool nextCalled = false;

        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var config = BuildConfig(null);

        var middleware = new ApiKeyAuthMiddleware(next, config, logger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_HealthPath_SkipsAuth()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/health";
        var logger = new Mock<ILogger<ApiKeyAuthMiddleware>>();
        bool nextCalled = false;

        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var config = BuildConfig("secret-key");

        var middleware = new ApiKeyAuthMiddleware(next, config, logger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_SwaggerPath_SkipsAuth()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/swagger/v1/swagger.json";
        var logger = new Mock<ILogger<ApiKeyAuthMiddleware>>();
        bool nextCalled = false;

        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var config = BuildConfig("secret-key");

        var middleware = new ApiKeyAuthMiddleware(next, config, logger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_RootPath_SkipsAuth()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/";
        var logger = new Mock<ILogger<ApiKeyAuthMiddleware>>();
        bool nextCalled = false;

        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var config = BuildConfig("secret-key");

        var middleware = new ApiKeyAuthMiddleware(next, config, logger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_MissingApiKeyHeader_Returns401()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/ask";
        context.Response.Body = new MemoryStream();
        var logger = new Mock<ILogger<ApiKeyAuthMiddleware>>();
        bool nextCalled = false;

        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var config = BuildConfig("secret-key");

        var middleware = new ApiKeyAuthMiddleware(next, config, logger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task InvokeAsync_InvalidApiKey_Returns403()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/ask";
        context.Request.Headers["X-Api-Key"] = "wrong-key";
        context.Response.Body = new MemoryStream();
        var logger = new Mock<ILogger<ApiKeyAuthMiddleware>>();
        bool nextCalled = false;

        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var config = BuildConfig("secret-key");

        var middleware = new ApiKeyAuthMiddleware(next, config, logger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task InvokeAsync_ValidApiKey_CallsNext()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/ask";
        context.Request.Headers["X-Api-Key"] = "correct-key";
        var logger = new Mock<ILogger<ApiKeyAuthMiddleware>>();
        bool nextCalled = false;

        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var config = BuildConfig("correct-key");

        var middleware = new ApiKeyAuthMiddleware(next, config, logger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_ReadyPath_SkipsAuth()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/ready";
        var logger = new Mock<ILogger<ApiKeyAuthMiddleware>>();
        bool nextCalled = false;

        RequestDelegate next = _ => { nextCalled = true; return Task.CompletedTask; };
        var config = BuildConfig("secret-key");

        var middleware = new ApiKeyAuthMiddleware(next, config, logger.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }
}
