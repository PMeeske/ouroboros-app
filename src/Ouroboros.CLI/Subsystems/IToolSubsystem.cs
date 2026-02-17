using Ouroboros.Application.Mcp;
using Ouroboros.Application.Tools;
using Ouroboros.CLI.Commands;
using Ouroboros.Pipeline.WorldModel;

namespace Ouroboros.CLI.Subsystems;

/// <summary>
/// Manages tool registry, dynamic tool creation, smart tool selection, and browser automation.
/// </summary>
public interface IToolSubsystem : IAgentSubsystem
{
    ToolRegistry Tools { get; set; }
    DynamicToolFactory? ToolFactory { get; }
    IntelligentToolLearner? ToolLearner { get; }
    SmartToolSelector? SmartToolSelector { get; set; }
    ToolCapabilityMatcher? ToolCapabilityMatcher { get; set; }
    PlaywrightMcpTool? PlaywrightTool { get; }
    PromptOptimizer PromptOptimizer { get; }

    // Pipeline DSL state
    IReadOnlyDictionary<string, PipelineTokenInfo>? AllPipelineTokens { get; }
    CliPipelineState? PipelineState { get; set; }
}