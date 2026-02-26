using Ouroboros.Options;

namespace Ouroboros.Tests.CLI.Options;

[Trait("Category", "Unit")]
public class NetworkOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var options = new NetworkOptions();

        options.CreateNode.Should().BeFalse();
        options.TypeName.Should().BeNull();
        options.Payload.Should().BeNull();
        options.AddTransition.Should().BeFalse();
        options.InputId.Should().BeNull();
        options.OutputId.Should().BeNull();
        options.OperationName.Should().BeNull();
        options.ViewDag.Should().BeFalse();
        options.CreateSnapshot.Should().BeFalse();
        options.ReplayToNode.Should().BeNull();
        options.ListNodes.Should().BeFalse();
        options.ListEdges.Should().BeFalse();
        options.Interactive.Should().BeFalse();
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        var options = new NetworkOptions
        {
            CreateNode = true,
            TypeName = "Draft",
            Payload = "{\"text\":\"hello\"}",
            AddTransition = true,
            InputId = "node-1",
            OutputId = "node-2",
            OperationName = "refine",
            ViewDag = true,
            CreateSnapshot = true,
            ReplayToNode = "node-3",
            ListNodes = true,
            ListEdges = true,
            Interactive = true
        };

        options.CreateNode.Should().BeTrue();
        options.TypeName.Should().Be("Draft");
        options.Payload.Should().Be("{\"text\":\"hello\"}");
        options.AddTransition.Should().BeTrue();
        options.InputId.Should().Be("node-1");
        options.OutputId.Should().Be("node-2");
        options.OperationName.Should().Be("refine");
        options.ViewDag.Should().BeTrue();
        options.CreateSnapshot.Should().BeTrue();
        options.ReplayToNode.Should().Be("node-3");
        options.ListNodes.Should().BeTrue();
        options.ListEdges.Should().BeTrue();
        options.Interactive.Should().BeTrue();
    }
}
