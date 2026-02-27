using System.CommandLine;
using Ouroboros.Application.Configuration;

namespace Ouroboros.CLI.Commands.Options;

/// <summary>
/// Piping, Debug, Cost, Avatar, Collective Mind, Election, Room Presence,
/// and OpenClaw Gateway options for the ouroboros agent command.
/// </summary>
public partial class OuroborosCommandOptions
{
    // ═══════════════════════════════════════════════════════════════════════════
    // PIPING & BATCH MODE
    // ═══════════════════════════════════════════════════════════════════════════

    public Option<bool> PipeOption { get; } = new("--pipe")
    {
        Description = "Enable pipe mode - read commands from stdin, output to stdout",
        DefaultValueFactory = _ => false
    };

    public Option<string?> BatchFileOption { get; } = new("--batch")
    {
        Description = "Batch file containing commands to execute (one per line)"
    };

    public Option<bool> JsonOutputOption { get; } = new("--json-output")
    {
        Description = "Output responses as JSON for scripting",
        DefaultValueFactory = _ => false
    };

    public Option<bool> NoGreetingOption { get; } = new("--no-greeting")
    {
        Description = "Skip greeting in non-interactive mode",
        DefaultValueFactory = _ => false
    };

    public Option<bool> ExitOnErrorOption { get; } = new("--exit-on-error")
    {
        Description = "Exit immediately on command error in batch/pipe mode",
        DefaultValueFactory = _ => false
    };

    public Option<string?> ExecOption { get; } = new("--exec", "-e")
    {
        Description = "Execute a single command and exit (supports | piping syntax)"
    };

    // ═══════════════════════════════════════════════════════════════════════════
    // DEBUG & OUTPUT
    // ═══════════════════════════════════════════════════════════════════════════

    public Option<bool> DebugOption { get; } = new("--debug")
    {
        Description = "Enable debug logging",
        DefaultValueFactory = _ => false
    };

    public Option<bool> TraceOption { get; } = new("--trace")
    {
        Description = "Enable trace output for pipelines",
        DefaultValueFactory = _ => false
    };

    public Option<bool> MetricsOption { get; } = new("--metrics")
    {
        Description = "Show performance metrics",
        DefaultValueFactory = _ => false
    };

    public Option<bool> StreamOption { get; } = new("--stream")
    {
        Description = "Stream responses as generated",
        DefaultValueFactory = _ => true
    };

    public Option<bool> VerboseOption { get; } = new("--verbose")
    {
        Description = "Show full initialization output and debug messages",
        DefaultValueFactory = _ => false
    };

    public Option<bool> QuietOption { get; } = new("--quiet")
    {
        Description = "Suppress all system messages, show responses only",
        DefaultValueFactory = _ => false
    };

    // ═══════════════════════════════════════════════════════════════════════════
    // COST TRACKING & EFFICIENCY
    // ═══════════════════════════════════════════════════════════════════════════

    public Option<bool> ShowCostsOption { get; } = new("--show-costs")
    {
        Description = "Display token counts and API costs after each response",
        DefaultValueFactory = _ => false
    };

    public Option<bool> CostAwareOption { get; } = new("--cost-aware")
    {
        Description = "Inject cost-awareness guidelines into system prompt",
        DefaultValueFactory = _ => false
    };

    public Option<bool> CostSummaryOption { get; } = new("--cost-summary")
    {
        Description = "Show session cost summary on exit",
        DefaultValueFactory = _ => true
    };

    // ═══════════════════════════════════════════════════════════════════════════
    // COLLECTIVE MIND (Multi-Provider)
    // ═══════════════════════════════════════════════════════════════════════════

    public Option<bool> CollectiveModeOption { get; } = new("--collective")
    {
        Description = "Enable collective mind mode (uses multiple LLM providers)",
        DefaultValueFactory = _ => false
    };

    public Option<string?> CollectivePresetOption { get; } = new("--collective-preset")
    {
        Description = "Collective mind preset: single|local|balanced|fast|premium|budget|anthropic-ollama|anthropic-ollama-lite"
    };

    public Option<string> CollectiveThinkingModeOption { get; } = new("--collective-mode")
    {
        Description = "Collective thinking mode: racing|sequential|ensemble|adaptive",
        DefaultValueFactory = _ => "adaptive"
    };

    public Option<string?> CollectiveProvidersOption { get; } = new("--collective-providers")
    {
        Description = "Comma-separated list of providers (e.g., anthropic,openai,deepseek,groq,ollama)"
    };

    public Option<bool> FailoverOption { get; } = new("--failover")
    {
        Description = "Enable automatic failover to other providers on error",
        DefaultValueFactory = _ => true
    };

    // ═══════════════════════════════════════════════════════════════════════════
    // ELECTION & ORCHESTRATION
    // ═══════════════════════════════════════════════════════════════════════════

    public Option<string> ElectionStrategyOption { get; } = new("--election")
    {
        Description = "Election strategy: majority|weighted|borda|condorcet|runoff|approval|master",
        DefaultValueFactory = _ => "weighted"
    };

    public Option<string?> MasterModelOption { get; } = new("--master")
    {
        Description = "Designate master model for orchestration"
    };

    public Option<string> EvalCriteriaOption { get; } = new("--eval-criteria")
    {
        Description = "Evaluation criteria preset: default|quality|speed|cost",
        DefaultValueFactory = _ => "default"
    };

    public Option<bool> ShowElectionOption { get; } = new("--show-election")
    {
        Description = "Show election results and voting details",
        DefaultValueFactory = _ => false
    };

    public Option<bool> ShowOptimizationOption { get; } = new("--show-optimization")
    {
        Description = "Show model optimization suggestions after session",
        DefaultValueFactory = _ => false
    };

    // ═══════════════════════════════════════════════════════════════════════════
    // INTERACTIVE AVATAR
    // ═══════════════════════════════════════════════════════════════════════════

    public Option<bool> AvatarOption { get; } = new("--avatar")
    {
        Description = "Launch interactive avatar viewer (auto-opens browser)",
        DefaultValueFactory = _ => true
    };

    public Option<int> AvatarPortOption { get; } = new("--avatar-port")
    {
        Description = "Override avatar viewer port (default: auto-assign from 9471)",
        DefaultValueFactory = _ => 0
    };

    public Option<bool> AvatarCloudOption { get; } = new("--avatar-cloud")
    {
        Description = "Enable Stability AI cloud frame generation (requires credits)",
        DefaultValueFactory = _ => false
    };

    public Option<string?> SdEndpointOption { get; } = new("--sd-endpoint")
    {
        Description = "Stable Diffusion (Forge/A1111) endpoint for avatar video stream (default: http://localhost:7860)"
    };

    public Option<string?> SdModelOption { get; } = new("--sd-model")
    {
        Description = "Stable Diffusion model name for avatar video stream (default: stable-diffusion)"
    };

    // ── Room Presence ─────────────────────────────────────────────────────

    public Option<bool> RoomModeOption { get; } = new("--room-mode")
    {
        Description = "Run ambient room-presence listener alongside the normal interactive session",
        DefaultValueFactory = _ => false
    };

    // ── OpenClaw Gateway ──────────────────────────────────────────────────────

    public Option<bool> EnableOpenClawOption { get; } = new("--enable-openclaw")
    {
        Description = "Connect to the OpenClaw Gateway for messaging and device node integration",
        DefaultValueFactory = _ => true
    };

    public Option<string?> OpenClawGatewayOption { get; } = new("--openclaw-gateway")
    {
        Description = "OpenClaw Gateway WebSocket URL (default: ws://127.0.0.1:18789)",
        DefaultValueFactory = _ => DefaultEndpoints.OpenClawGateway
    };

    public Option<string?> OpenClawTokenOption { get; } = new("--openclaw-token")
    {
        Description = "OpenClaw Gateway auth token (or set OPENCLAW_TOKEN env var)",
    };

    public Option<bool> EnablePcNodeOption { get; } = new("--enable-pc-node")
    {
        Description = "Enable PC node mode (register this machine as an OpenClaw device node)",
        DefaultValueFactory = _ => false
    };

    public Option<string?> PcNodeConfigOption { get; } = new("--pc-node-config")
    {
        Description = "Path to PC node security configuration JSON file",
    };
}
