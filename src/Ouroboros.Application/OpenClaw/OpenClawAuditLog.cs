// <copyright file="OpenClawAuditLog.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Collections.Concurrent;

namespace Ouroboros.Application.OpenClaw;

/// <summary>
/// Append-only audit trail for all OpenClaw Gateway operations.
///
/// Records every allowed and denied tool invocation with timestamps, channels,
/// recipients, and verdicts. Useful for post-hoc security review and debugging.
///
/// Thread-safe. Entries are bounded by <see cref="MaxEntries"/> to prevent
/// unbounded memory growth in long-running sessions.
/// </summary>
public sealed class OpenClawAuditLog
{
    private readonly ConcurrentQueue<AuditEntry> _entries = new();
    private int _count;

    /// <summary>
    /// Maximum entries to retain. Oldest entries are evicted when full.
    /// </summary>
    public int MaxEntries { get; }

    /// <summary>
    /// Total number of operations recorded since creation (including evicted entries).
    /// </summary>
    public long TotalOperations => _totalAllowed + _totalDenied;

    private long _totalAllowed;
    private long _totalDenied;

    /// <summary>
    /// Total number of allowed operations since creation.
    /// </summary>
    public long TotalAllowed => Interlocked.Read(ref _totalAllowed);

    /// <summary>
    /// Total number of denied operations since creation.
    /// </summary>
    public long TotalDenied => Interlocked.Read(ref _totalDenied);

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenClawAuditLog"/> class.
    /// </summary>
    /// <param name="maxEntries">Maximum entries to retain in memory (default: 1000).</param>
    public OpenClawAuditLog(int maxEntries = 1000)
    {
        MaxEntries = maxEntries > 0 ? maxEntries : 1000;
    }

    /// <summary>
    /// Records an allowed operation.
    /// </summary>
    public void LogAllowed(string toolName, string channel, string target, string detail)
    {
        Append(new AuditEntry
        {
            Timestamp = DateTime.UtcNow,
            ToolName = toolName,
            Channel = channel,
            Target = MaskTarget(target),
            Detail = detail,
            Verdict = AuditVerdict.Allowed,
            DenyReason = null,
        });
        Interlocked.Increment(ref _totalAllowed);
    }

    /// <summary>
    /// Records a denied operation.
    /// </summary>
    public void LogDenied(string toolName, string channel, string target, string reason)
    {
        Append(new AuditEntry
        {
            Timestamp = DateTime.UtcNow,
            ToolName = toolName,
            Channel = channel,
            Target = MaskTarget(target),
            Detail = null,
            Verdict = AuditVerdict.Denied,
            DenyReason = reason,
        });
        Interlocked.Increment(ref _totalDenied);
    }

    /// <summary>
    /// Returns all retained audit entries, newest first.
    /// </summary>
    public IReadOnlyList<AuditEntry> GetEntries() =>
        _entries.Reverse().ToList().AsReadOnly();

    /// <summary>
    /// Returns a summary suitable for display in the agent's status output.
    /// </summary>
    public string GetSummary()
    {
        var recent = _entries.TakeLast(5).Reverse().ToList();
        var lines = new List<string>
        {
            $"OpenClaw Audit: {TotalAllowed} allowed, {TotalDenied} denied ({_entries.Count} entries retained)",
        };

        if (recent.Count > 0)
        {
            lines.Add("Recent:");
            foreach (var entry in recent)
            {
                var verdict = entry.Verdict == AuditVerdict.Allowed ? "OK" : "DENY";
                var time = entry.Timestamp.ToString("HH:mm:ss");
                lines.Add($"  [{time}] {verdict} {entry.ToolName} → {entry.Channel}/{entry.Target}" +
                         (entry.DenyReason != null ? $" ({entry.DenyReason})" : ""));
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    // ── Internal ────────────────────────────────────────────────────────────────

    private void Append(AuditEntry entry)
    {
        _entries.Enqueue(entry);

        // Evict oldest entries if over capacity.
        // ConcurrentQueue.Count is O(1) in .NET 6+; avoids Interlocked races.
        while (_entries.Count > MaxEntries)
        {
            if (!_entries.TryDequeue(out _))
                break;
        }
    }

    /// <summary>
    /// Masks targets for audit safety (PII reduction in logs).
    /// </summary>
    private static string MaskTarget(string target)
    {
        if (string.IsNullOrEmpty(target) || target.Length < 6)
            return "***";

        // Phone numbers: show first 4 and last 4
        if (target.StartsWith('+') && target.Length >= 10)
            return target[..4] + new string('*', target.Length - 8) + target[^4..];

        // Email: mask local part
        var atIndex = target.IndexOf('@');
        if (atIndex > 0)
            return target[..1] + "***" + target[atIndex..];

        // Generic: show first 3 and last 2
        if (target.Length > 8)
            return target[..3] + "***" + target[^2..];

        return target;
    }
}

/// <summary>
/// A single entry in the OpenClaw audit trail.
/// </summary>
public sealed record AuditEntry
{
    /// <summary>UTC timestamp of the operation.</summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>Tool name (e.g., "openclaw_send_message", "openclaw_node_invoke").</summary>
    public required string ToolName { get; init; }

    /// <summary>Channel or node identifier.</summary>
    public required string Channel { get; init; }

    /// <summary>Masked target (recipient, command).</summary>
    public required string Target { get; init; }

    /// <summary>Additional detail (message length, params, etc.).</summary>
    public string? Detail { get; init; }

    /// <summary>Whether the operation was allowed or denied.</summary>
    public required AuditVerdict Verdict { get; init; }

    /// <summary>Reason for denial (null if allowed).</summary>
    public string? DenyReason { get; init; }
}

/// <summary>
/// Audit verdict for an OpenClaw operation.
/// </summary>
public enum AuditVerdict
{
    /// <summary>The operation was permitted by the security policy.</summary>
    Allowed,

    /// <summary>The operation was blocked by the security policy.</summary>
    Denied,
}
