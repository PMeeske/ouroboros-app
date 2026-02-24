// <copyright file="OpenClawSecurityConfig.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.OpenClaw;

/// <summary>
/// Configuration for the OpenClaw security policy.
/// All allowlists default to empty (deny-all). The operator must explicitly
/// enable channels, recipients, and node commands.
/// </summary>
public sealed class OpenClawSecurityConfig
{
    // ── Channel Allowlist ───────────────────────────────────────────────────────

    /// <summary>
    /// Set of channels the agent is permitted to send messages through.
    /// Empty = no channels allowed (fail-closed).
    /// Example: { "whatsapp", "telegram", "slack" }
    /// </summary>
    public HashSet<string> AllowedChannels { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // ── Recipient Allowlist ─────────────────────────────────────────────────────

    /// <summary>
    /// Per-channel recipient allowlists. Key = channel name, Value = allowed recipients.
    /// If a channel has no entry here, any recipient on that channel is allowed
    /// (as long as the channel itself is in <see cref="AllowedChannels"/>).
    /// If a channel has an entry with a non-empty set, only those recipients are allowed.
    /// Example: { "whatsapp": { "+15551234567" }, "sms": { "+15559876543" } }
    /// </summary>
    public Dictionary<string, HashSet<string>> AllowedRecipients { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // ── Node Command Allowlist ──────────────────────────────────────────────────

    /// <summary>
    /// Set of node commands the agent is permitted to invoke.
    /// Supports wildcards: "camera.*" allows camera.snap, camera.clip, etc.
    /// Empty = no commands allowed (fail-closed).
    /// Example: { "camera.*", "location.get", "screen.record" }
    /// </summary>
    public HashSet<string> AllowedNodeCommands { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // ── Rate Limiting ───────────────────────────────────────────────────────────

    /// <summary>
    /// Maximum number of messages across all channels within the rate-limit window.
    /// Default: 20 messages per window.
    /// </summary>
    public int GlobalRateLimitPerWindow { get; set; } = 20;

    /// <summary>
    /// Maximum number of messages per individual channel within the rate-limit window.
    /// Default: 10 messages per channel per window.
    /// </summary>
    public int ChannelRateLimitPerWindow { get; set; } = 10;

    /// <summary>
    /// Duration of the rate-limit sliding window in seconds.
    /// Default: 60 seconds.
    /// </summary>
    public int RateLimitWindowSeconds { get; set; } = 60;

    // ── Content Limits ──────────────────────────────────────────────────────────

    /// <summary>
    /// Maximum message length in characters. Messages exceeding this are rejected.
    /// Default: 4096 characters.
    /// </summary>
    public int MaxMessageLength { get; set; } = 4096;

    // ── Sensitive Data Detection ────────────────────────────────────────────────

    /// <summary>
    /// Whether to scan outbound messages for sensitive data patterns
    /// (API keys, tokens, passwords, PII). Default: true.
    /// </summary>
    public bool EnableSensitiveDataScan { get; set; } = true;

    // ── Audit ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Whether to write an audit log of all OpenClaw operations.
    /// Default: true.
    /// </summary>
    public bool EnableAuditLog { get; set; } = true;

    /// <summary>
    /// Maximum number of audit log entries to retain in memory.
    /// Older entries are evicted on a FIFO basis.
    /// Default: 1000.
    /// </summary>
    public int MaxAuditLogEntries { get; set; } = 1000;

    // ── Factory ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a permissive config useful for local development / testing.
    /// All common channels enabled, all safe node commands enabled,
    /// sensitive data scan still active.
    /// </summary>
    public static OpenClawSecurityConfig CreateDevelopment() => new()
    {
        AllowedChannels = new(StringComparer.OrdinalIgnoreCase)
        {
            "whatsapp", "telegram", "discord", "slack", "signal",
            "imessage", "webchat", "matrix", "google-chat", "teams",
        },
        AllowedNodeCommands = new(StringComparer.OrdinalIgnoreCase)
        {
            "camera.*", "location.get", "screen.record",
            "canvas.*", "system.notify",
        },
        GlobalRateLimitPerWindow = 60,
        ChannelRateLimitPerWindow = 30,
        EnableSensitiveDataScan = true,
        EnableAuditLog = true,
    };

    /// <summary>
    /// Creates a locked-down config (the default). Nothing is allowed until
    /// the operator explicitly adds channels and commands.
    /// </summary>
    public static OpenClawSecurityConfig CreateDefault() => new();
}
