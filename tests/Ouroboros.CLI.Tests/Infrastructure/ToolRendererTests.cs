using Ouroboros.CLI.Infrastructure;

namespace Ouroboros.Tests.CLI.Infrastructure;

[Trait("Category", "Unit")]
public class ToolRendererTests
{
    [Fact]
    public void DefaultBodyLines_IsTen()
    {
        ToolRenderer.DefaultBodyLines.Should().Be(10);
    }

    [Fact]
    public void WriteToolStart_WithParam_DoesNotThrow()
    {
        var mockOutput = new Moq.Mock<IConsoleOutput>();

        var action = () => ToolRenderer.WriteToolStart(mockOutput.Object, "TestTool", "param1");

        action.Should().NotThrow();
        mockOutput.Verify(o => o.WriteSystem(Moq.It.Is<string>(s =>
            s.Contains("TestTool") && s.Contains("param1"))), Moq.Times.Once);
    }

    [Fact]
    public void WriteToolStart_WithoutParam_DoesNotThrow()
    {
        var mockOutput = new Moq.Mock<IConsoleOutput>();

        ToolRenderer.WriteToolStart(mockOutput.Object, "TestTool");

        mockOutput.Verify(o => o.WriteSystem(Moq.It.Is<string>(s =>
            s.Contains("TestTool"))), Moq.Times.Once);
    }

    [Fact]
    public void WriteToolDone_Success_WritesCheckmark()
    {
        var mockOutput = new Moq.Mock<IConsoleOutput>();

        ToolRenderer.WriteToolDone(mockOutput.Object, "TestTool", success: true);

        mockOutput.Verify(o => o.WriteSystem(Moq.It.IsAny<string>()), Moq.Times.Once);
    }

    [Fact]
    public void WriteToolDone_Failure_WritesCross()
    {
        var mockOutput = new Moq.Mock<IConsoleOutput>();

        ToolRenderer.WriteToolDone(mockOutput.Object, "TestTool", success: false);

        mockOutput.Verify(o => o.WriteSystem(Moq.It.IsAny<string>()), Moq.Times.Once);
    }

    [Fact]
    public void WriteToolCancelled_WritesOutput()
    {
        var mockOutput = new Moq.Mock<IConsoleOutput>();

        ToolRenderer.WriteToolCancelled(mockOutput.Object, "TestTool");

        mockOutput.Verify(o => o.WriteSystem(Moq.It.IsAny<string>()), Moq.Times.Once);
    }
}
