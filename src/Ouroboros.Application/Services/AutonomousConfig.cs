namespace Ouroboros.Application.Services;

/// <summary>
/// Configuration for autonomous behavior.
/// </summary>
public class AutonomousConfig
{
    /// <summary>
    /// Seconds between autonomous thoughts.
    /// </summary>
    public int ThinkingIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Seconds between curiosity-driven searches.
    /// </summary>
    public int CuriosityIntervalSeconds { get; set; } = 120;

    /// <summary>
    /// Seconds between autonomous action executions.
    /// </summary>
    public int ActionIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Seconds between state persistence operations.
    /// </summary>
    public int PersistenceIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Number of persistence cycles between full knowledge reorganizations.
    /// Default: 10 cycles (~10 minutes with default persistence interval).
    /// </summary>
    public int ReorganizationCycleInterval { get; set; } = 10;

    /// <summary>
    /// Minimum minutes between full reorganizations.
    /// Prevents too frequent reorganizations during rapid activity.
    /// </summary>
    public int MinReorganizationIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Probability of sharing discoveries with user (0-1).
    /// </summary>
    public double ShareDiscoveryProbability { get; set; } = 0.3;

    /// <summary>
    /// Whether to report autonomous actions.
    /// </summary>
    public bool ReportActions { get; set; } = true;

    /// <summary>
    /// Tools allowed for autonomous execution.
    /// </summary>
    public HashSet<string> AllowedAutonomousTools { get; set; } =
    [
        "capture_screen",
        "get_active_window",
        "get_mouse_position",
        "list_captured_images",
        "search_indexed_content",
        "search_my_code",
        "read_my_file",
        "modify_my_code",
        "system_info",
        "disk_info",
        "network_info",
        "list_dir",
    ];
}