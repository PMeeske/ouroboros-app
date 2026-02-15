# Ouroboros CLI Migration Plan

## Overview
This document outlines the incremental migration plan to refactor the existing CLI application to use modern .NET 8 patterns including System.CommandLine, Microsoft.Extensions.Hosting, Dependency Injection, and Spectre.Console.

## Migration Requirements

### Step 1: Introduce Host.CreateDefaultBuilder as Composition Root
- [x] Create `Program.cs` with HostBuilder
- [x] Create service registration extensions
- [x] Register existing services in DI container
- [ ] Test basic host startup

### Step 2: Replace Manual Argument Parsing with System.CommandLine
- [x] Create RootCommand structure
- [x] Add subcommands for existing verbs
- [x] Create command handlers
- [ ] Migrate existing option classes
- [ ] Test command parsing

### Step 3: Replace Console Output with Spectre.Console
- [x] Create SpectreConsoleService wrapper
- [x] Update command handlers to use Spectre.Console
- [ ] Replace Console.WriteLine calls
- [ ] Test rich terminal output

### Step 4: Add Global "--voice" Option Integration
- [x] Create VoiceIntegrationService
- [x] Add global voice option
- [x] Implement speech recognition flow
- [ ] Test voice command handling
- [ ] Add cancellation support

## Safe Rollout Strategy

### Phase 1: Parallel Operation (Week 1-2)
- Keep existing CommandLineParser working
- Add new System.CommandLine commands alongside
- Use feature flags to enable/disable new implementation
- Run integration tests on both implementations

### Phase 2: Gradual Migration (Week 3-4)
- Migrate one command at a time
- Test thoroughly before moving to next command
- Maintain backward compatibility
- Update documentation

### Phase 3: Full Cutover (Week 5)
- Remove old CommandLineParser dependency
- Remove feature flags
- Final testing and validation
- Update CI/CD pipelines

## Unit Test Strategy

### Command Handler Tests
```csharp
public class AskCommandHandlerTests
{
    [Fact]
    public async Task AskCommand_WithValidQuestion_ReturnsAnswer()
    {
        // Arrange
        var mockAskService = new Mock<IAskService>();
        mockAskService.Setup(s => s.AskAsync("test", false))
            .ReturnsAsync("test answer");
        
        var handler = new AskCommandHandler(mockAskService.Object);
        
        // Act
        var result = await handler.HandleAsync("test", false);
        
        // Assert
        Assert.Equal("test answer", result);
    }
}
```

### Voice Integration Tests
```csharp
public class VoiceIntegrationServiceTests
{
    [Fact]
    public async Task HandleVoiceCommandAsync_WhenVoiceAvailable_RecognizesSpeech()
    {
        // Arrange
        var mockVoiceService = new Mock<VoiceModeService>();
        mockVoiceService.Setup(v => v.HasStt).Returns(true);
        mockVoiceService.Setup(v => v.GetInputAsync(It.IsAny<string>()))
            .ReturnsAsync("test question");
        
        var service = new VoiceIntegrationService(mockVoiceService.Object, ...);
        
        // Act
        await service.HandleVoiceCommandAsync("ask", Array.Empty<string>());
        
        // Assert
        mockVoiceService.Verify(v => v.GetInputAsync(It.IsAny<string>()), Times.Once);
    }
}
```

### Spectre.Console Integration Tests
```csharp
public class SpectreConsoleServiceTests
{
    [Fact]
    public void MarkupLine_WithValidMarkup_WritesToConsole()
    {
        // Arrange
        var console = new TestConsole();
        var service = new SpectreConsoleService(console);
        
        // Act
        service.MarkupLine("[green]test[/]");
        
        // Assert
        Assert.Contains("test", console.Output);
    }
}
```

## Cancellation Handling Strategy

### Command-Level Cancellation
```csharp
command.SetHandler(async (context) =>
{
    var cancellationToken = context.GetCancellationToken();
    
    await console.Status().StartAsync("Processing...", async ctx =>
    {
        try
        {
            var result = await service.ProcessAsync(input, cancellationToken);
            ctx.Status = "Done";
            console.MarkupLine($"[green]Result:[/] {result}");
        }
        catch (OperationCanceledException)
        {
            console.MarkupLine("[yellow]Operation cancelled[/]");
        }
    });
});
```

### Voice Recognition Cancellation
```csharp
public async Task<string[]> RecognizeSpeechAsync(CancellationToken cancellationToken = default)
{
    try
    {
        return await _voiceModeService.GetInputAsync("Speak now: ", cancellationToken)
            .ContinueWith(task => ParseSpeechToArguments(task.Result), cancellationToken);
    }
    catch (OperationCanceledException)
    {
        _logger.LogInformation("Speech recognition cancelled");
        return Array.Empty<string>();
    }
}
```

## Domain/Application/Infrastructure Layer Separation

### Domain Layer
- Command definitions
- Option classes
- Business entities

### Application Layer
- Command handlers
- Service interfaces
- Business logic

### Infrastructure Layer
- Spectre.Console integration
- Voice recognition
- External service clients

## Risk Mitigation

### Backward Compatibility
- Maintain existing CommandLineParser during migration
- Ensure all existing options are supported
- Test with existing scripts and workflows

### Performance Impact
- Monitor startup time with HostBuilder
- Profile command execution
- Optimize service registration

### Voice Integration Stability
- Graceful fallback when voice services unavailable
- Comprehensive error handling
- User-friendly error messages

## Success Metrics

- All existing commands work identically
- Voice integration works reliably
- Rich terminal output enhances UX
- Codebase is more maintainable
- Unit test coverage increases
- Performance remains acceptable

## Next Steps

1. Implement command handlers with extracted business logic
2. Migrate existing option classes to System.CommandLine
3. Add comprehensive unit tests
4. Perform integration testing
5. Update documentation
6. Deploy and monitor