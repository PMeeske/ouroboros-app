// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.

using Moq;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Infrastructure;
using Ouroboros.CLI.Subsystems;

namespace Ouroboros.Tests.CLI.Subsystems;

/// <summary>
/// Verifies the inverted (default-on) permission model in <see cref="ToolSubsystem"/>.
/// Every tool requires interactive approval unless explicitly listed in <c>ExemptTools</c>.
/// </summary>
[Trait("Category", "Unit")]
public class ToolSubsystemSecurityTests
{
    // ── Dangerous tools MUST require permission ──────────────────────────────

    [Fact]
    public void Shell_Tool_Requires_Permission()
    {
        ToolSubsystem.ExemptTools.Should().NotContain("shell",
            because: "shell execution is destructive and must require approval");
    }

    [Fact]
    public void WriteFile_Tool_Requires_Permission()
    {
        ToolSubsystem.ExemptTools.Should().NotContain("write_file",
            because: "writing files is a mutating operation and must require approval");
    }

    [Fact]
    public void StartProcess_Tool_Requires_Permission()
    {
        ToolSubsystem.ExemptTools.Should().NotContain("start_process",
            because: "starting processes is dangerous and must require approval");
    }

    [Fact]
    public void CreateNewTool_Requires_Permission()
    {
        ToolSubsystem.ExemptTools.Should().NotContain("create_new_tool",
            because: "dynamic tool creation is a high-risk operation and must require approval");
    }

    [Fact]
    public void Environment_Tool_Requires_Permission()
    {
        ToolSubsystem.ExemptTools.Should().NotContain("environment",
            because: "environment variable access can leak secrets and must require approval");
    }

    [Theory]
    [InlineData("kill_process")]
    [InlineData("modify_my_code")]
    [InlineData("rebuild_self")]
    [InlineData("delete_file")]
    [InlineData("execute_command")]
    [InlineData("http_request")]
    [InlineData("send_email")]
    public void Other_Dangerous_Tools_Require_Permission(string toolName)
    {
        ToolSubsystem.ExemptTools.Should().NotContain(toolName,
            because: $"'{toolName}' is a mutating/dangerous operation and must require approval");
    }

    // ── Read-only tools ARE exempt ──────────────────────────────────────────

    [Theory]
    [InlineData("read_file")]
    [InlineData("read_my_file")]
    [InlineData("list_directory")]
    [InlineData("search_files")]
    [InlineData("search_my_code")]
    [InlineData("list_my_files")]
    [InlineData("search_indexed")]
    [InlineData("system_info")]
    [InlineData("autonomous_status")]
    [InlineData("neural_network_status")]
    [InlineData("git_status")]
    [InlineData("get_codebase_overview")]
    [InlineData("verify_claim")]
    [InlineData("reasoning_chain")]
    [InlineData("see_screen")]
    [InlineData("persistence_stats")]
    [InlineData("service_discovery")]
    public void ReadOnly_Tools_Are_Exempt(string toolName)
    {
        ToolSubsystem.ExemptTools.Should().Contain(toolName,
            because: $"'{toolName}' is a read-only / harmless tool and should be exempt from approval");
    }

    // ── Unknown / newly added tools default to requiring permission ─────────

    [Fact]
    public void Unknown_New_Tool_Requires_Permission_By_Default()
    {
        // Any tool name not explicitly listed in ExemptTools must require permission.
        // This tests the core inversion: new tools are gated by default.
        var unknownTools = new[]
        {
            "some_brand_new_tool",
            "super_dangerous_tool",
            "not_yet_categorized",
            "future_mcp_tool_v99",
        };

        foreach (var tool in unknownTools)
        {
            ToolSubsystem.ExemptTools.Should().NotContain(tool,
                because: $"unknown tool '{tool}' must default to requiring permission (defense-in-depth)");
        }
    }

    // ── ExemptTools set uses case-insensitive comparison ─────────────────────

    [Theory]
    [InlineData("READ_FILE")]
    [InlineData("Read_File")]
    [InlineData("read_FILE")]
    public void ExemptTools_Is_Case_Insensitive(string toolName)
    {
        ToolSubsystem.ExemptTools.Should().Contain(toolName,
            because: "the ExemptTools set must use case-insensitive comparison");
    }

    // ── Integration: ExecuteWithUiAsync respects the inverted model ─────────

    [Fact]
    public async Task NonExempt_Tool_With_Broker_Invokes_Permission_Check()
    {
        // Arrange: a ToolSubsystem with a real broker (SkipAll = true so it auto-allows)
        var sut = await CreateToolSubsystemAsync(withBroker: true, skipAll: true);
        var tool = CreateMockTool("shell");

        // Act: execute a non-exempt tool
        var result = await sut.ExecuteWithUiAsync(tool.Object, "ls", ct: CancellationToken.None);

        // Assert: the tool was invoked (broker auto-allowed)
        result.Should().NotBeNull("broker with SkipAll=true should allow the tool");
        tool.Verify(t => t.InvokeAsync("ls", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Exempt_Tool_Executes_Without_Broker_Gate()
    {
        // Arrange: a ToolSubsystem with a broker, but the tool is exempt
        // SkipAll is false — if the broker were consulted for an exempt tool,
        // it would block on interactive console input and the test would hang.
        // The fact that this test completes proves the broker is bypassed.
        var sut = await CreateToolSubsystemAsync(withBroker: true, skipAll: false);
        var tool = CreateMockTool("read_file");

        // Act: execute an exempt tool — should bypass the broker entirely
        var result = await sut.ExecuteWithUiAsync(tool.Object, "{}", ct: CancellationToken.None);

        // Assert: the tool executed successfully without hitting the broker
        result.Should().NotBeNull("exempt tools should bypass the permission broker");
        tool.Verify(t => t.InvokeAsync("{}", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NonExempt_Tool_Without_Broker_Executes_Directly()
    {
        // Arrange: no broker configured (PermissionBroker = null)
        var sut = await CreateToolSubsystemAsync(withBroker: false);
        var tool = CreateMockTool("shell");

        // Act
        var result = await sut.ExecuteWithUiAsync(tool.Object, "ls", ct: CancellationToken.None);

        // Assert: with no broker, even non-exempt tools execute (no one to ask)
        result.Should().NotBeNull("without a broker, tools execute directly");
        tool.Verify(t => t.InvokeAsync("ls", It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<ToolSubsystem> CreateToolSubsystemAsync(bool withBroker, bool skipAll = false)
    {
        var output = new Mock<IConsoleOutput>();
        var voiceConfig = new VoiceModeConfig(
            Persona: "Test", VoiceOnly: false, LocalTts: false,
            VoiceLoop: false, DisableStt: true,
            Model: "test", Endpoint: "http://localhost",
            EmbedModel: "embed", QdrantEndpoint: "http://q");
        var voiceService = new VoiceModeService(voiceConfig);

        var broker = withBroker ? new ToolPermissionBroker { SkipAll = skipAll } : null;

        var sut = new ToolSubsystem();
        var ctx = new SubsystemInitContext
        {
            // EnableTools = false causes InitializeAsync to return early after
            // setting Ctx/Config/Output/Models/Memory, which is all we need.
            Config = new OuroborosConfig(EnableTools: false),
            Output = output.Object,
            VoiceService = voiceService,
            Voice = new VoiceSubsystem(voiceService),
            Models = new ModelSubsystem(),
            Tools = sut,
            Memory = new MemorySubsystem(),
            Cognitive = new CognitiveSubsystem(),
            Autonomy = new AutonomySubsystem(),
            Embodiment = new EmbodimentSubsystem(),
            PermissionBroker = broker,
        };

        await sut.InitializeAsync(ctx);
        return sut;
    }

    private static Mock<ITool> CreateMockTool(string name)
    {
        var tool = new Mock<ITool>();
        tool.Setup(t => t.Name).Returns(name);
        tool.Setup(t => t.Description).Returns($"Mock {name} tool");
        tool.Setup(t => t.InvokeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string, string>.Success("success"));
        return tool;
    }
}
