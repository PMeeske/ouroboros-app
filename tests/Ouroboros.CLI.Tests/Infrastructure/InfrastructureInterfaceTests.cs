using Ouroboros.CLI.Infrastructure;

namespace Ouroboros.Tests.CLI.Infrastructure;

/// <summary>
/// Verifies that all infrastructure interfaces are properly defined.
/// </summary>
[Trait("Category", "Unit")]
public class InfrastructureInterfaceTests
{
    [Fact]
    public void IConsoleOutput_IsInterface()
    {
        typeof(IConsoleOutput).IsInterface.Should().BeTrue();
    }

    [Fact]
    public void ISpinnerHandle_ExtendsIDisposable()
    {
        typeof(ISpinnerHandle).Should().Implement<IDisposable>();
    }

    [Fact]
    public void ISpinnerHandle_HasUpdateLabelMethod()
    {
        typeof(ISpinnerHandle).GetMethod("UpdateLabel").Should().NotBeNull();
    }

    [Fact]
    public void IAgentEventSink_IsInterface()
    {
        typeof(IAgentEventSink).IsInterface.Should().BeTrue();
    }

    [Fact]
    public void IAgentEventSink_HasEnqueueMethod()
    {
        typeof(IAgentEventSink).GetMethod("Enqueue").Should().NotBeNull();
    }

    [Fact]
    public void IAgentEventSink_HasPendingCountProperty()
    {
        typeof(IAgentEventSink).GetProperty("PendingCount").Should().NotBeNull();
    }

    [Fact]
    public void ISpectreConsoleService_IsInterface()
    {
        typeof(ISpectreConsoleService).IsInterface.Should().BeTrue();
    }

    [Fact]
    public void IVoiceIntegrationService_IsInterface()
    {
        typeof(IVoiceIntegrationService).IsInterface.Should().BeTrue();
    }

    [Fact]
    public void IVoiceIntegrationService_HasHandleVoiceCommandAsync()
    {
        typeof(IVoiceIntegrationService).GetMethod("HandleVoiceCommandAsync").Should().NotBeNull();
    }

    [Fact]
    public void IVoiceIntegrationService_HasIsVoiceRecognitionAvailableAsync()
    {
        typeof(IVoiceIntegrationService).GetMethod("IsVoiceRecognitionAvailableAsync").Should().NotBeNull();
    }
}
