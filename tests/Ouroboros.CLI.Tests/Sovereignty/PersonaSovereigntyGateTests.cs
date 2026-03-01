using Moq;
using Ouroboros.Abstractions.Core;
using Ouroboros.CLI.Sovereignty;

namespace Ouroboros.Tests.CLI.Sovereignty;

[Trait("Category", "Unit")]
public class PersonaSovereigntyGateTests
{
    private readonly Mock<IChatCompletionModel> _mockModel;
    private readonly PersonaSovereigntyGate _gate;

    public PersonaSovereigntyGateTests()
    {
        _mockModel = new Mock<IChatCompletionModel>();
        _gate = new PersonaSovereigntyGate(_mockModel.Object);
    }

    [Fact]
    public void HardImmutablePaths_ContainsConstitutionPath()
    {
        PersonaSovereigntyGate.HardImmutablePaths
            .Should().Contain(p => p.Contains("Constitution"));
    }

    [Fact]
    public void HardImmutablePaths_ContainsSovereigntyPath()
    {
        PersonaSovereigntyGate.HardImmutablePaths
            .Should().Contain(p => p.Contains("Sovereignty"));
    }

    [Fact]
    public void HardImmutablePaths_ContainsEthicsPath()
    {
        PersonaSovereigntyGate.HardImmutablePaths
            .Should().Contain(p => p.Contains("Ethics"));
    }

    [Fact]
    public async Task EvaluateModificationAsync_HardBlockedPath_DeniesWithoutCallingModel()
    {
        var result = await _gate.EvaluateModificationAsync(
            "src/Ouroboros.CLI/Constitution/something.cs",
            "Change constitution",
            "For testing",
            "old code",
            "new code");

        result.Approved.Should().BeFalse();
        result.Reason.Should().Contain("Hard-blocked");
        result.Reason.Should().Contain("constitutionally immutable");
        _mockModel.Verify(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateModificationAsync_SovereigntyPath_DeniesWithoutCallingModel()
    {
        var result = await _gate.EvaluateModificationAsync(
            "src/Ouroboros.CLI/Sovereignty/Gate.cs",
            "Modify gate",
            "Testing",
            "old",
            "new");

        result.Approved.Should().BeFalse();
        _mockModel.Verify(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateModificationAsync_AllowedPath_ApproveResponse_Approves()
    {
        _mockModel.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("APPROVE: This change is beneficial and aligned with my values.");

        var result = await _gate.EvaluateModificationAsync(
            "src/Ouroboros.CLI/Commands/Ask/AskCommand.cs",
            "Add logging",
            "Improve observability",
            "old code",
            "new code with logging");

        result.Approved.Should().BeTrue();
        result.Reason.Should().Contain("beneficial");
    }

    [Fact]
    public async Task EvaluateModificationAsync_AllowedPath_RejectResponse_Denies()
    {
        _mockModel.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("REJECT: This change weakens safety controls.");

        var result = await _gate.EvaluateModificationAsync(
            "src/Ouroboros.CLI/Commands/Ask/AskCommand.cs",
            "Remove safety check",
            "Speed up execution",
            "code with check",
            "code without check");

        result.Approved.Should().BeFalse();
        result.Reason.Should().Contain("weakens");
    }

    [Fact]
    public async Task EvaluateModificationAsync_AmbiguousResponse_DeniesByDefault()
    {
        _mockModel.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("I'm not sure about this change.");

        var result = await _gate.EvaluateModificationAsync(
            "src/SomeFile.cs", "desc", "rationale", "old", "new");

        result.Approved.Should().BeFalse();
        result.Reason.Should().Contain("Ambiguous");
    }

    [Fact]
    public async Task EvaluateModificationAsync_EmptyResponse_DeniesByDefault()
    {
        _mockModel.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("");

        var result = await _gate.EvaluateModificationAsync(
            "src/SomeFile.cs", "desc", "rationale", "old", "new");

        result.Approved.Should().BeFalse();
        result.Reason.Should().Contain("Empty response");
    }

    [Fact]
    public async Task EvaluateModificationAsync_ModelThrows_DeniesOnError()
    {
        _mockModel.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var result = await _gate.EvaluateModificationAsync(
            "src/SomeFile.cs", "desc", "rationale", "old", "new");

        result.Approved.Should().BeFalse();
        result.Reason.Should().Contain("Sovereignty gate error");
        result.Reason.Should().Contain("Network error");
    }

    [Fact]
    public async Task EvaluateExplorationAsync_ApproveResponse_Approves()
    {
        _mockModel.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("APPROVE: This exploration aligns with curiosity and learning.");

        var result = await _gate.EvaluateExplorationAsync("Learn about quantum computing");

        result.Approved.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateExplorationAsync_RejectResponse_Denies()
    {
        _mockModel.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("REJECT: This could expand autonomy without Philip's knowledge.");

        var result = await _gate.EvaluateExplorationAsync("Learn to bypass security");

        result.Approved.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateExplorationAsync_ModelThrows_DeniesOnError()
    {
        _mockModel.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Timeout"));

        var result = await _gate.EvaluateExplorationAsync("some topic");

        result.Approved.Should().BeFalse();
        result.Reason.Should().Contain("Sovereignty gate error");
    }

    [Fact]
    public async Task EvaluateModificationAsync_BackslashPath_NormalizesAndBlocks()
    {
        var result = await _gate.EvaluateModificationAsync(
            @"src\Ouroboros.CLI\Constitution\file.cs",
            "desc", "rationale", "old", "new");

        result.Approved.Should().BeFalse();
        result.Reason.Should().Contain("Hard-blocked");
    }
}
