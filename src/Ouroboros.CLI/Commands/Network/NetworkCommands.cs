using Ouroboros.CLI.Avatar;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.Network;
using Ouroboros.Options;
using Spectre.Console;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// CLI commands for interacting with the emergent network state.
/// </summary>
public static class NetworkCommands
{
    private static readonly MerkleDag Dag = new();
    private static readonly NetworkStateProjector Projector = new(Dag);
    private static readonly TransitionReplayEngine ReplayEngine = new(Dag);

    /// <summary>
    /// Executes network state commands based on the provided options.
    /// </summary>
    public static async Task RunAsync(NetworkOptions options)
    {
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Emergent Network State Manager"));
        AnsiConsole.WriteLine();

        try
        {
            if (options.Interactive)
            {
                await RunInteractiveDemoAsync();
                return;
            }

            if (options.CreateNode)
            {
                CreateNode(options);
            }
            else if (options.AddTransition)
            {
                AddTransition(options);
            }
            else if (options.ViewDag)
            {
                ViewDag(options);
            }
            else if (options.CreateSnapshot)
            {
                CreateAndDisplaySnapshot();
            }
            else if (!string.IsNullOrEmpty(options.ReplayToNode))
            {
                ReplayTransitions(options.ReplayToNode);
            }
            else if (options.ListNodes)
            {
                ListNodes(options);
            }
            else if (options.ListEdges)
            {
                ListEdges();
            }
            else
            {
                AnsiConsole.MarkupLine(OuroborosTheme.Dim("Please specify a command. Use --help for options."));
            }
        }
        catch (InvalidOperationException ex)
        {
            PrintError($"Error: {ex.Message}");
        }
    }

    private static void CreateNode(NetworkOptions options)
    {
        if (string.IsNullOrEmpty(options.TypeName) || string.IsNullOrEmpty(options.Payload))
        {
            PrintError("--type and --payload are required for creating a node.");
            return;
        }

        var node = MonadNode.FromPayload(options.TypeName, options.Payload);
        var result = Dag.AddNode(node);

        if (result.IsSuccess)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Ok($"✓ Created node: {node.Id}"));
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Type:")} {Markup.Escape(node.TypeName)}");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Hash:")} {Markup.Escape(node.Hash)}");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Created:")} {node.CreatedAt}");
        }
        else
        {
            PrintError($"Failed to create node: {result.Error}");
        }
    }

    private static void AddTransition(NetworkOptions options)
    {
        if (string.IsNullOrEmpty(options.InputId) ||
            string.IsNullOrEmpty(options.OutputId) ||
            string.IsNullOrEmpty(options.OperationName))
        {
            PrintError("--input, --output, and --operation are required for adding a transition.");
            return;
        }

        if (!Guid.TryParse(options.InputId, out var inputGuid) ||
            !Guid.TryParse(options.OutputId, out var outputGuid))
        {
            PrintError("Invalid node ID format.");
            return;
        }

        var edge = TransitionEdge.CreateSimple(
            inputGuid,
            outputGuid,
            options.OperationName,
            new { Timestamp = DateTimeOffset.UtcNow });

        var result = Dag.AddEdge(edge);

        if (result.IsSuccess)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Ok($"✓ Created transition: {edge.Id}"));
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Operation:")} {Markup.Escape(edge.OperationName)}");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Input:")} {edge.InputIds[0]}");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Output:")} {edge.OutputId}");
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Hash:")} {Markup.Escape(edge.Hash)}");
        }
        else
        {
            PrintError($"Failed to create transition: {result.Error}");
        }
    }

    private static void ViewDag(NetworkOptions options)
    {
        var table = OuroborosTheme.ThemedTable("Metric", "Value");
        table.AddRow("Total Nodes", $"{Dag.NodeCount}");
        table.AddRow("Total Transitions", $"{Dag.EdgeCount}");
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        var rootNodes = Dag.GetRootNodes().ToList();
        var leafNodes = Dag.GetLeafNodes().ToList();

        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent($"Root Nodes ({rootNodes.Count}):")}");
        foreach (var node in rootNodes)
        {
            AnsiConsole.MarkupLine($"    - {node.Id} ({Markup.Escape(node.TypeName)})");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent($"Leaf Nodes ({leafNodes.Count}):")}");
        foreach (var node in leafNodes)
        {
            AnsiConsole.MarkupLine($"    - {node.Id} ({Markup.Escape(node.TypeName)})");
        }

        // Verify integrity
        var integrityResult = Dag.VerifyIntegrity();
        AnsiConsole.WriteLine();
        if (integrityResult.IsSuccess)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Ok("✓ DAG integrity verified"));
        }
        else
        {
            PrintError($"DAG integrity check failed: {integrityResult.Error}");
        }
    }

    private static void CreateAndDisplaySnapshot()
    {
        var snapshot = Projector.CreateSnapshot();

        AnsiConsole.Write(OuroborosTheme.ThemedRule("Global Network State Snapshot"));
        AnsiConsole.WriteLine();

        var table = OuroborosTheme.ThemedTable("Metric", "Value");
        table.AddRow("Epoch", $"{snapshot.Epoch}");
        table.AddRow("Timestamp", $"{snapshot.Timestamp}");
        table.AddRow("Total Nodes", $"{snapshot.TotalNodes}");
        table.AddRow("Total Transitions", $"{snapshot.TotalTransitions}");
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        if (snapshot.NodeCountByType.Count > 0)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Accent("Nodes by Type:"));
            foreach (var kvp in snapshot.NodeCountByType)
            {
                AnsiConsole.MarkupLine($"    {Markup.Escape(kvp.Key)}: {kvp.Value}");
            }
        }

        AnsiConsole.WriteLine();

        if (snapshot.TransitionCountByOperation.Count > 0)
        {
            AnsiConsole.MarkupLine(OuroborosTheme.Accent("Transitions by Operation:"));
            foreach (var kvp in snapshot.TransitionCountByOperation)
            {
                AnsiConsole.MarkupLine($"    {Markup.Escape(kvp.Key)}: {kvp.Value}");
            }
        }

        if (snapshot.AverageConfidence.HasValue)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Average Confidence:")} {snapshot.AverageConfidence.Value:F2}");
        }

        if (snapshot.TotalProcessingTimeMs.HasValue)
        {
            AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Total Processing Time:")} {snapshot.TotalProcessingTimeMs.Value}ms");
        }
    }

    private static void ReplayTransitions(string nodeIdStr)
    {
        if (!Guid.TryParse(nodeIdStr, out var nodeId))
        {
            PrintError("Invalid node ID format.");
            return;
        }

        var result = ReplayEngine.ReplayPathToNode(nodeId);

        if (result.IsSuccess)
        {
            AnsiConsole.Write(OuroborosTheme.ThemedRule($"Transition Path to {nodeId}"));
            AnsiConsole.WriteLine();

            for (var i = 0; i < result.Value.Length; i++)
            {
                var edge = result.Value[i];
                AnsiConsole.MarkupLine($"  {OuroborosTheme.GoldText($"Step {i + 1}:")} {Markup.Escape(edge.OperationName)}");
                AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("ID:")} {edge.Id}");
                AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Input:")} {edge.InputIds[0]}");
                AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Output:")} {edge.OutputId}");
                AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Created:")} {edge.CreatedAt}");
                AnsiConsole.WriteLine();
            }
        }
        else
        {
            PrintError($"Failed to replay: {result.Error}");
        }
    }

    private static void ListNodes(NetworkOptions options)
    {
        IEnumerable<MonadNode> nodes = Dag.Nodes.Values;

        if (!string.IsNullOrEmpty(options.TypeName))
        {
            nodes = Dag.GetNodesByType(options.TypeName);
            AnsiConsole.Write(OuroborosTheme.ThemedRule($"Nodes of type '{options.TypeName}'"));
        }
        else
        {
            AnsiConsole.Write(OuroborosTheme.ThemedRule("All Nodes"));
        }
        AnsiConsole.WriteLine();

        foreach (var node in nodes)
        {
            AnsiConsole.MarkupLine($"  {OuroborosTheme.GoldText("ID:")} {node.Id}");
            AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Type:")} {Markup.Escape(node.TypeName)}");
            AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Created:")} {node.CreatedAt}");
            AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Parents:")} {node.ParentIds.Length}");
            AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Hash:")} {Markup.Escape(node.Hash[..16])}...");
            AnsiConsole.WriteLine();
        }

        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent($"Total: {nodes.Count()} nodes")}");
    }

    private static void ListEdges()
    {
        AnsiConsole.Write(OuroborosTheme.ThemedRule("All Transitions"));
        AnsiConsole.WriteLine();

        foreach (var edge in Dag.Edges.Values)
        {
            AnsiConsole.MarkupLine($"  {OuroborosTheme.GoldText("ID:")} {edge.Id}");
            AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Operation:")} {Markup.Escape(edge.OperationName)}");
            AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Input(s):")} {Markup.Escape(string.Join(", ", edge.InputIds))}");
            AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Output:")} {edge.OutputId}");
            AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Created:")} {edge.CreatedAt}");
            if (edge.Confidence.HasValue)
            {
                AnsiConsole.MarkupLine($"    {OuroborosTheme.Accent("Confidence:")} {edge.Confidence.Value:F2}");
            }
            AnsiConsole.WriteLine();
        }

        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent($"Total: {Dag.EdgeCount} transitions")}");
    }

    private static async Task RunInteractiveDemoAsync()
    {
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Interactive Network State Demo"));
        AnsiConsole.MarkupLine(OuroborosTheme.Dim("Creating a reasoning chain: Draft → Critique → Improve → Final"));
        AnsiConsole.WriteLine();

        // Create Draft node
        var draft = new Draft("Initial implementation of the feature");
        var draftNode = MonadNode.FromReasoningState(draft);
        Dag.AddNode(draftNode);
        AnsiConsole.MarkupLine($"  {OuroborosTheme.GoldText("1.")} Created Draft node: {draftNode.Id}");
        await Task.Delay(500);

        // Create Critique node
        var critique = new Critique("The implementation lacks error handling and edge case validation");
        var critiqueNode = MonadNode.FromReasoningState(critique, ImmutableArray.Create(draftNode.Id));
        Dag.AddNode(critiqueNode);
        AnsiConsole.MarkupLine($"  {OuroborosTheme.GoldText("2.")} Created Critique node: {critiqueNode.Id}");
        await Task.Delay(500);

        // Add transition: Draft → Critique
        var edge1 = TransitionEdge.CreateSimple(
            draftNode.Id,
            critiqueNode.Id,
            "UseCritique",
            new { Prompt = "Analyze and critique the draft" },
            confidence: 0.85,
            durationMs: 1200);
        Dag.AddEdge(edge1);
        AnsiConsole.MarkupLine($"  {OuroborosTheme.GoldText("3.")} Added transition: Draft → Critique");
        await Task.Delay(500);

        // Create Improved node
        var improved = new Draft("Enhanced implementation with comprehensive error handling and validation");
        var improvedNode = MonadNode.FromReasoningState(improved, ImmutableArray.Create(critiqueNode.Id));
        Dag.AddNode(improvedNode);
        AnsiConsole.MarkupLine($"  {OuroborosTheme.GoldText("4.")} Created Improved Draft node: {improvedNode.Id}");
        await Task.Delay(500);

        // Add transition: Critique → Improved
        var edge2 = TransitionEdge.CreateSimple(
            critiqueNode.Id,
            improvedNode.Id,
            "UseImprove",
            new { Prompt = "Improve based on critique" },
            confidence: 0.92,
            durationMs: 1500);
        Dag.AddEdge(edge2);
        AnsiConsole.MarkupLine($"  {OuroborosTheme.GoldText("5.")} Added transition: Critique → Improved");
        await Task.Delay(500);

        // Create Final node
        var final = new FinalSpec("Production-ready implementation with full error handling, validation, and documentation");
        var finalNode = MonadNode.FromReasoningState(final, ImmutableArray.Create(improvedNode.Id));
        Dag.AddNode(finalNode);
        AnsiConsole.MarkupLine($"  {OuroborosTheme.GoldText("6.")} Created Final node: {finalNode.Id}");
        await Task.Delay(500);

        // Add transition: Improved → Final
        var edge3 = TransitionEdge.CreateSimple(
            improvedNode.Id,
            finalNode.Id,
            "Finalize",
            new { Prompt = "Finalize the specification" },
            confidence: 0.95,
            durationMs: 800);
        Dag.AddEdge(edge3);
        AnsiConsole.MarkupLine($"  {OuroborosTheme.GoldText("7.")} Added transition: Improved → Final");
        AnsiConsole.WriteLine();

        // Display DAG summary
        AnsiConsole.Write(OuroborosTheme.ThemedRule("DAG Summary"));
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Total Nodes:")} {Dag.NodeCount}");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Total Transitions:")} {Dag.EdgeCount}");
        AnsiConsole.WriteLine();

        // Create snapshot
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Global Network State Snapshot"));
        var snapshot = Projector.CreateSnapshot(
            ImmutableDictionary<string, string>.Empty.Add("demo", "reasoning-chain"));

        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Epoch:")} {snapshot.Epoch}");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Total Nodes:")} {snapshot.TotalNodes}");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Total Transitions:")} {snapshot.TotalTransitions}");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Average Confidence:")} {snapshot.AverageConfidence:F2}");
        AnsiConsole.MarkupLine($"  {OuroborosTheme.Accent("Total Processing Time:")} {snapshot.TotalProcessingTimeMs}ms");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine(OuroborosTheme.Accent("Nodes by Type:"));
        foreach (var kvp in snapshot.NodeCountByType)
        {
            AnsiConsole.MarkupLine($"    {Markup.Escape(kvp.Key)}: {kvp.Value}");
        }
        AnsiConsole.WriteLine();

        // Replay path
        AnsiConsole.Write(OuroborosTheme.ThemedRule("Replaying Path to Final Node"));
        var replayResult = ReplayEngine.ReplayPathToNode(finalNode.Id);
        if (replayResult.IsSuccess)
        {
            for (var i = 0; i < replayResult.Value.Length; i++)
            {
                var edge = replayResult.Value[i];
                AnsiConsole.MarkupLine($"  {OuroborosTheme.GoldText($"Step {i + 1}:")} {Markup.Escape(edge.OperationName)} (Confidence: {edge.Confidence:F2})");
            }
        }

        AnsiConsole.WriteLine();
        var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Happy);
        AnsiConsole.MarkupLine(OuroborosTheme.Ok($"{face} Demo completed successfully!"));
    }

    private static void PrintError(string message)
    {
        var face = IaretCliAvatar.Inline(IaretCliAvatar.Expression.Concerned);
        AnsiConsole.MarkupLine($"  [red]{Markup.Escape(face)} ✗ {Markup.Escape(message)}[/]");
    }
}
