using System.CommandLine;

namespace Ouroboros.CLI.Commands.Options;

/// <summary>
/// Feature Toggles, Autonomous/Push Mode, and Governance options for the ouroboros agent command.
/// </summary>
public partial class OuroborosCommandOptions
{
    // ═══════════════════════════════════════════════════════════════════════════
    // FEATURE TOGGLES (All enabled by default for max experience)
    // ═══════════════════════════════════════════════════════════════════════════

    public Option<bool> NoSkillsOption { get; } = new("--no-skills")
    {
        Description = "Disable skill learning subsystem",
        DefaultValueFactory = _ => false
    };

    public Option<bool> NoMeTTaOption { get; } = new("--no-metta")
    {
        Description = "Disable MeTTa symbolic reasoning",
        DefaultValueFactory = _ => false
    };

    public Option<bool> NoToolsOption { get; } = new("--no-tools")
    {
        Description = "Disable dynamic tools (web search, etc.)",
        DefaultValueFactory = _ => false
    };

    public Option<bool> NoPersonalityOption { get; } = new("--no-personality")
    {
        Description = "Disable personality engine & affect",
        DefaultValueFactory = _ => false
    };

    public Option<bool> NoMindOption { get; } = new("--no-mind")
    {
        Description = "Disable autonomous mind (inner thoughts)",
        DefaultValueFactory = _ => false
    };

    public Option<bool> NoBrowserOption { get; } = new("--no-browser")
    {
        Description = "Disable Playwright browser automation",
        DefaultValueFactory = _ => false
    };

    // ═══════════════════════════════════════════════════════════════════════════
    // AUTONOMOUS/PUSH MODE
    // ═══════════════════════════════════════════════════════════════════════════

    public Option<bool> PushOption { get; } = new("--push")
    {
        Description = "Enable push mode - Ouroboros proposes actions for your approval",
        DefaultValueFactory = _ => false
    };

    public Option<bool> PushVoiceOption { get; } = new("--push-voice")
    {
        Description = "Enable voice in push mode",
        DefaultValueFactory = _ => false
    };

    public Option<bool> YoloOption { get; } = new("--yolo")
    {
        Description = "YOLO mode - full autonomous operation, auto-approve ALL actions (use with caution!)",
        DefaultValueFactory = _ => false
    };

    public Option<string> AutoApproveOption { get; } = new("--auto-approve")
    {
        Description = "Auto-approve intention categories: safe,memory,analysis (comma-separated)",
        DefaultValueFactory = _ => ""
    };

    public Option<int> IntentionIntervalOption { get; } = new("--intention-interval")
    {
        Description = "Seconds between autonomous intention proposals",
        DefaultValueFactory = _ => 45
    };

    public Option<int> DiscoveryIntervalOption { get; } = new("--discovery-interval")
    {
        Description = "Seconds between autonomous topic discovery",
        DefaultValueFactory = _ => 90
    };

    // ═══════════════════════════════════════════════════════════════════════════
    // GOVERNANCE & SELF-MODIFICATION
    // ═══════════════════════════════════════════════════════════════════════════

    public Option<bool> EnableSelfModOption { get; } = new("--enable-self-mod")
    {
        Description = "Enable self-modification for agent autonomy",
        DefaultValueFactory = _ => false
    };

    public Option<string> RiskLevelOption { get; } = new("--risk-level")
    {
        Description = "Minimum risk level for approval: Low|Medium|High|Critical",
        DefaultValueFactory = _ => "Medium"
    };

    public Option<bool> AutoApproveLowOption { get; } = new("--auto-approve-low")
    {
        Description = "Auto-approve low-risk modifications",
        DefaultValueFactory = _ => true
    };

    // ═══════════════════════════════════════════════════════════════════════════
    // INITIAL TASK (Optional)
    // ═══════════════════════════════════════════════════════════════════════════

    public Option<string?> GoalOption { get; } = new("--goal", "-g")
    {
        Description = "Initial goal to accomplish (starts planning immediately)"
    };

    public Option<string?> QuestionOption { get; } = new("--question", "-q")
    {
        Description = "Initial question to answer"
    };

    public Option<string?> DslOption { get; } = new("--dsl", "-d")
    {
        Description = "Pipeline DSL to execute immediately"
    };
}
