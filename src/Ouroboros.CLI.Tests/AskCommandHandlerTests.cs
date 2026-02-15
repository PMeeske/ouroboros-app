using Microsoft.Extensions.Logging;
using Moq;
using Ouroboros.CLI.Services;
using Xunit;

namespace Ouroboros.CLI.Tests;

public class AskCommandHandlerTests
{
    [Fact]
    public async Task AskAsync_WithValidQuestion_ReturnsAnswer()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AskService>>();
        var service = new AskService(mockLogger.Object);
        
        // Act
        var result = await service.AskAsync("test question", false);
        
        // Assert
        Assert.Contains("test question", result);
        Assert.Contains("RAG: False", result);
    }
    
    [Fact]
    public async Task AskAsync_WithRagEnabled_IncludesRagInResult()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AskService>>();
        var service = new AskService(mockLogger.Object);
        
        // Act
        var result = await service.AskAsync("test question", true);
        
        // Assert
        Assert.Contains("test question", result);
        Assert.Contains("RAG: True", result);
    }
    
    [Fact]
    public async Task AskAsync_WithNullQuestion_ReturnsEmptyAnswer()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<AskService>>();
        var service = new AskService(mockLogger.Object);
        
        // Act
        var result = await service.AskAsync(null!, false);
        
        // Assert
        Assert.NotNull(result);
        Assert.Contains("RAG: False", result);
    }
}