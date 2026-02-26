// <copyright file="PcNodeSecurityConfig.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Ouroboros.Application.OpenClaw.PcNode;

/// <summary>
/// Security configuration for the PC node. Controls which capabilities are enabled,
/// file path restrictions, application whitelists, shell command rules, and rate limits.
///
/// Design principle: fail-closed. Empty allowlists = nothing permitted.
/// </summary>
public sealed class PcNodeSecurityConfig
{
    // ── Capability Enablement ──────────────────────────────────────────────────

    /// <summary>
    /// Set of capability names that are enabled on this node.
    /// Empty = no capabilities available (fail-closed).
    /// Example: { "system.info", "screen.capture", "clipboard.read" }
    /// </summary>
    public HashSet<string> EnabledCapabilities { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // ── Caller Authorization ───────────────────────────────────────────────────

    /// <summary>
    /// Device IDs allowed to invoke capabilities on this node.
    /// Empty = deny all callers (fail-closed).
    /// Use "*" as a single entry to allow any authenticated caller.
    /// </summary>
    public HashSet<string> AllowedCallerDeviceIds { get; set; } = new();

    // ── File System Restrictions ───────────────────────────────────────────────

    /// <summary>
    /// Base directories where file operations (read/write/list/delete) are permitted.
    /// Paths are canonicalized before comparison. Empty = no file access.
    /// Example: [ "D:\\Projects", "C:\\Users\\phil\\Documents" ]
    /// </summary>
    public List<string> AllowedFileDirectories { get; set; } = [];

    /// <summary>
    /// Paths that are always blocked, even if under an allowed directory.
    /// Supports environment variable expansion (e.g., "%APPDATA%").
    /// </summary>
    public List<string> BlockedFilePaths { get; set; } =
    [
        @"C:\Windows",
        @"C:\Program Files",
        @"C:\Program Files (x86)",
        "%APPDATA%",
        "%LOCALAPPDATA%",
    ];

    /// <summary>
    /// Maximum file size in bytes for read/write operations.
    /// Default: 10 MB.
    /// </summary>
    public long MaxFileSize { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// File extensions that can never be written or deleted.
    /// Prevents accidental creation of executables.
    /// </summary>
    public HashSet<string> BlockedFileExtensions { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".msi",
        ".dll", ".sys", ".com", ".scr", ".pif", ".wsf", ".wsh",
    };

    // ── Application Whitelist ──────────────────────────────────────────────────

    /// <summary>
    /// Application names (without extension) that may be launched via app.launch.
    /// Empty = no applications can be launched (fail-closed).
    /// Example: { "notepad", "code", "chrome" }
    /// </summary>
    public HashSet<string> AllowedApplications { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Process names that may never be killed via process.kill.
    /// These are always protected regardless of other settings.
    /// </summary>
    public HashSet<string> ProtectedProcesses { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "csrss", "wininit", "winlogon", "smss", "services", "lsass",
        "svchost", "System", "dwm", "explorer", "taskmgr",
    };

    // ── URL Restrictions ───────────────────────────────────────────────────────

    /// <summary>
    /// URL schemes allowed for browser.open. Default: http, https only.
    /// </summary>
    public HashSet<string> AllowedUrlSchemes { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        "http", "https",
    };

    /// <summary>
    /// Domains blocked from being opened in the browser.
    /// </summary>
    public HashSet<string> BlockedUrlDomains { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // ── Shell Command Restrictions ─────────────────────────────────────────────

    /// <summary>
    /// Master switch for shell command execution. Default: disabled.
    /// Even when enabled, commands must pass the allowlist and blocklist checks.
    /// </summary>
    public bool EnableShellCommands { get; set; } = false;

    /// <summary>
    /// Specific shell commands or command prefixes that are permitted.
    /// Only checked when <see cref="EnableShellCommands"/> is true.
    /// Example: { "git status", "dotnet build", "ping" }
    /// </summary>
    public HashSet<string> AllowedShellCommands { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Regex patterns that are always blocked in shell commands.
    /// Applied even when a command matches the allowlist.
    /// </summary>
    public List<string> BlockedShellPatterns { get; set; } =
    [
        @"rm\s+-rf",
        @"del\s+/[sfq]",
        @"format\s+[a-zA-Z]:",
        @"reg\s+delete",
        @"net\s+user",
        @"netsh\s+",
        @"takeown\s+",
        @"icacls\s+",
        @"shutdown\s+",
        @"bcdedit",
        @"diskpart",
        @"cipher\s+/w",
        @"sfc\s+/",
        @"dism\s+/",
    ];

    /// <summary>
    /// Maximum execution time for shell commands in seconds.
    /// Commands exceeding this are killed. Default: 30 seconds.
    /// </summary>
    public int ShellCommandTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum output size from shell commands in bytes.
    /// Output exceeding this is truncated. Default: 1 MB.
    /// </summary>
    public long ShellOutputMaxBytes { get; set; } = 1024 * 1024;

    // ── Screenshot / Recording ─────────────────────────────────────────────────

    /// <summary>
    /// Maximum screenshot dimension (width or height). Default: 1920px.
    /// </summary>
    public int MaxScreenshotResolution { get; set; } = 1920;

    /// <summary>
    /// Maximum screen recording duration in seconds. Default: 60s.
    /// </summary>
    public int MaxScreenRecordSeconds { get; set; } = 60;

    // ── Rate Limiting ──────────────────────────────────────────────────────────

    /// <summary>
    /// Maximum capability invocations per minute across all capabilities.
    /// Default: 30 per minute.
    /// </summary>
    public int GlobalRateLimitPerMinute { get; set; } = 30;

    /// <summary>
    /// Maximum high-risk capability invocations per minute.
    /// Default: 5 per minute.
    /// </summary>
    public int HighRiskRateLimitPerMinute { get; set; } = 5;

    // ── Approval ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Minimum risk level that requires interactive user approval.
    /// Capabilities at or above this level must be approved.
    /// Default: <see cref="PcNodeRiskLevel.High"/>.
    /// </summary>
    public PcNodeRiskLevel ApprovalThreshold { get; set; } = PcNodeRiskLevel.High;

    /// <summary>
    /// Maximum time in seconds to wait for user approval.
    /// Requests that time out are denied. Default: 60 seconds.
    /// </summary>
    public int ApprovalTimeoutSeconds { get; set; } = 60;

    // ── Content Limits ─────────────────────────────────────────────────────────

    /// <summary>
    /// Maximum clipboard content length in characters for clipboard.write.
    /// Default: 10,000 characters.
    /// </summary>
    public int MaxClipboardLength { get; set; } = 10_000;

    // ── Sensitive Data ─────────────────────────────────────────────────────────

    /// <summary>
    /// Whether to scan outbound results (file contents, clipboard data) for
    /// sensitive data patterns before returning them through the gateway.
    /// Default: true.
    /// </summary>
    public bool ScanOutboundResults { get; set; } = true;

    // ── Factory Methods ────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a locked-down config where nothing is enabled.
    /// This is the production default — the operator must explicitly configure capabilities.
    /// </summary>
    public static PcNodeSecurityConfig CreateDefault() => new();

    /// <summary>
    /// Creates a development config with safe, read-only capabilities enabled.
    /// Allows any authenticated caller. File access and shell commands remain disabled.
    /// </summary>
    public static PcNodeSecurityConfig CreateDevelopment() => new()
    {
        EnabledCapabilities = new(StringComparer.OrdinalIgnoreCase)
        {
            "system.info",
            "system.notify",
            "clipboard.read",
            "screen.capture",
            "process.list",
        },
        AllowedCallerDeviceIds = ["*"],
        GlobalRateLimitPerMinute = 60,
        HighRiskRateLimitPerMinute = 10,
    };

    /// <summary>
    /// Loads a configuration from a JSON file on disk.
    /// </summary>
    public static PcNodeSecurityConfig CreateFromFile(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<PcNodeSecurityConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        }) ?? new PcNodeSecurityConfig();
    }
}
