// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using System.Text.RegularExpressions;

public sealed partial class ToolSubsystem
{
    // ═══════════════════════════════════════════════════════════════════════════
    // SMART HOME + TOOL PERMISSION SETS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Parses natural language smart home command into tool input format.
    /// </summary>
    internal static string ParseSmartHomeCommand(string input)
    {
        if (input.Contains("list") && input.Contains("device"))
            return "list_devices";

        string action;
        if (input.Contains("set") && (input.Contains("color") || input.Contains("colour")))
            action = "set_color";
        else if (input.Contains("set") && input.Contains("bright"))
            action = "set_brightness";
        else if (input.Contains("device") && input.Contains("info"))
            action = "device_info";
        else if (input.Contains("turn") && input.Contains("off") || input.Contains("switch") && input.Contains("off"))
            action = "turn_off";
        else
            action = "turn_on";

        var deviceName = ExtractDeviceName(input);
        return $"{action} {deviceName}";
    }

    /// <summary>
    /// Extracts a device name from natural language input.
    /// </summary>
    internal static string ExtractDeviceName(string input)
    {
        var quoteMatch = Regex.Match(input, @"[""']([^""']+)[""']");
        if (quoteMatch.Success)
            return quoteMatch.Groups[1].Value.Trim();

        var afterAction = Regex.Match(input,
            @"\b(?:turn\s*(?:on|off)|switch\s*(?:on|off))\s+(?:the\s+)?(.+?)(?:\s+(?:light|lamp|plug|switch|bulb|strip))?$");
        if (afterAction.Success)
        {
            var raw = afterAction.Groups[1].Value.Trim();
            raw = Regex.Replace(raw, @"\s*(please|now|for me)\s*$", "").Trim();
            if (!string.IsNullOrEmpty(raw))
                return raw;
        }

        return input;
    }

    /// <summary>
    /// Tools that require interactive approval before execution.
    /// Matches Crush's "destructive / privacy-sensitive" classification.
    /// </summary>
    private static readonly HashSet<string> SensitiveTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "modify_my_code",
        "rebuild_self",
        "camera_ptz",
        "capture_camera",
        "smart_home",
        "openclaw_send_message",
        "openclaw_node_invoke",
        // OpenClaw session/spawn/device tools
        "openclaw_sessions_send",
        "openclaw_sessions_spawn",
        "openclaw_cron_add",
        "openclaw_cron_toggle",
        "openclaw_devices_approve",
        "openclaw_devices_revoke",
        "openclaw_camera_snap",
        "openclaw_camera_clip",
        "openclaw_location_get",
        "openclaw_screen_record_node",
        // PC node management
        "openclaw_pc_toggle_capability",
        "openclaw_approval_respond",
    };

    /// <summary>
    /// Read-only / harmless tools that are exempt from permission checks.
    /// Every tool NOT in this set requires interactive approval by default,
    /// providing defense-in-depth against newly added or unknown tools.
    /// </summary>
    internal static readonly HashSet<string> ExemptTools = new(StringComparer.OrdinalIgnoreCase)
    {
        // Status queries (read-only)
        "autonomous_status",
        "neural_network_status",
        "search_neuron_history",

        // Read-only file / code access
        "search_my_code",
        "read_my_file",
        "read_file",
        "list_directory",
        "list_my_files",
        "search_files",
        "search_indexed",

        // System info (read-only)
        "system_info",
        "list_processes",
        "disk_info",
        "network_info",

        // Safe cognitive tools
        "verify_claim",
        "reasoning_chain",
        "self_doubt",
        "compress_context",
        "episodic_memory",

        // Display / screen capture (passive observation)
        "see_screen",
        "read_screen_text",
        "list_captured_images",
        "capture_screen",
        "get_active_window",
        "get_mouse_position",

        // Persistence read / discovery
        "persistence_stats",
        "service_discovery",
        "git_status",
        "get_codebase_overview",

        // List-only / read-only helpers
        "list_my_intentions",
    };
}
