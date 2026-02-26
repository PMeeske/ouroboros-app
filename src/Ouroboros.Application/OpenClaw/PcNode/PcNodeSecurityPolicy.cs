// <copyright file="PcNodeSecurityPolicy.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ouroboros.Application.OpenClaw.PcNode;

/// <summary>
/// Security policy for inbound PC node capability invocations.
///
/// Validates every incoming request against:
///   1. Capability enablement — only configured capabilities may execute
///   2. Caller authorization — only known device IDs may invoke
///   3. Capability-specific validation (file paths, URLs, shell commands)
///   4. Rate limiting — global and per-risk-level throttling
///   5. Outbound sensitive data scanning — before returning results
///
/// Default stance: deny unless explicitly allowed (fail-closed).
/// </summary>
public sealed class PcNodeSecurityPolicy
{
    private readonly PcNodeSecurityConfig _config;
    private readonly OpenClawAuditLog _auditLog;

    // Rate-limiting state
    private readonly Queue<DateTime> _globalInvocations = new();
    private readonly Queue<DateTime> _highRiskInvocations = new();
    private readonly object _rateLimitLock = new();

    // Compiled blocklist patterns for shell commands
    private readonly Lazy<Regex[]> _blockedShellRegexes;

    public PcNodeSecurityPolicy(PcNodeSecurityConfig config, OpenClawAuditLog auditLog)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _auditLog = auditLog ?? throw new ArgumentNullException(nameof(auditLog));

        _blockedShellRegexes = new Lazy<Regex[]>(() =>
            _config.BlockedShellPatterns
                .Select(p => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled))
                .ToArray());
    }

    // ── Primary Validation ──────────────────────────────────────────────────────

    /// <summary>
    /// Validates an incoming node.invoke request before dispatch.
    /// </summary>
    public PolicyVerdict ValidateIncomingInvoke(
        string callerDeviceId,
        string capability,
        JsonElement? parameters,
        PcNodeRiskLevel riskLevel)
    {
        // 1. Capability enablement
        if (!_config.EnabledCapabilities.Contains(capability))
        {
            var verdict = PolicyVerdict.Deny($"Capability '{capability}' is not enabled on this node");
            _auditLog.LogDenied("pc_node_invoke", capability, callerDeviceId, verdict.Reason);
            return verdict;
        }

        // 2. Caller authorization
        if (!IsCallerAllowed(callerDeviceId))
        {
            var verdict = PolicyVerdict.Deny($"Caller device '{callerDeviceId[..Math.Min(8, callerDeviceId.Length)]}...' is not authorized");
            _auditLog.LogDenied("pc_node_invoke", capability, callerDeviceId, verdict.Reason);
            return verdict;
        }

        // 3. Rate limiting
        var rateLimitResult = CheckRateLimit(riskLevel);
        if (!rateLimitResult.IsAllowed)
        {
            var verdict = PolicyVerdict.Deny($"Rate limit exceeded: {rateLimitResult.Reason}");
            _auditLog.LogDenied("pc_node_invoke", capability, callerDeviceId, verdict.Reason);
            return verdict;
        }

        RecordInvocation(riskLevel);
        _auditLog.LogAllowed("pc_node_invoke", capability, callerDeviceId,
            parameters?.ToString()?.Length > 200
                ? parameters.Value.ToString()[..200] + "..."
                : parameters?.ToString() ?? "{}");

        return PolicyVerdict.Allow();
    }

    // ── File Path Validation ────────────────────────────────────────────────────

    /// <summary>
    /// Validates a file path against the path jail and blocked paths/extensions.
    /// </summary>
    public PolicyVerdict ValidateFilePath(string path, FileOperation operation)
    {
        try
        {
            var canonical = Path.GetFullPath(path);

            // Must be under an allowed directory
            bool isAllowed = _config.AllowedFileDirectories.Any(dir =>
                canonical.StartsWith(
                    Path.GetFullPath(dir),
                    StringComparison.OrdinalIgnoreCase));

            if (!isAllowed)
            {
                return PolicyVerdict.Deny(
                    $"Path is not in any allowed directory. Allowed: [{string.Join(", ", _config.AllowedFileDirectories)}]");
            }

            // Must not be in a blocked path
            foreach (var blocked in _config.BlockedFilePaths)
            {
                var expandedBlocked = Environment.ExpandEnvironmentVariables(blocked);
                if (canonical.StartsWith(
                    Path.GetFullPath(expandedBlocked),
                    StringComparison.OrdinalIgnoreCase))
                {
                    return PolicyVerdict.Deny($"Path is in a blocked area");
                }
            }

            // Extension check for write/delete operations
            if (operation is FileOperation.Write or FileOperation.Delete)
            {
                var ext = Path.GetExtension(canonical);
                if (!string.IsNullOrEmpty(ext) && _config.BlockedFileExtensions.Contains(ext))
                {
                    return PolicyVerdict.Deny($"File extension '{ext}' is blocked for {operation} operations");
                }
            }

            // Size check for reads (check file exists first)
            if (operation == FileOperation.Read && File.Exists(canonical))
            {
                var fileInfo = new FileInfo(canonical);
                if (fileInfo.Length > _config.MaxFileSize)
                {
                    return PolicyVerdict.Deny(
                        $"File size ({fileInfo.Length:N0} bytes) exceeds maximum ({_config.MaxFileSize:N0} bytes)");
                }
            }

            return PolicyVerdict.Allow();
        }
        catch (Exception ex)
        {
            // Fail-closed on any path resolution error
            return PolicyVerdict.Deny($"Path validation failed: {ex.Message}");
        }
    }

    // ── URL Validation ──────────────────────────────────────────────────────────

    /// <summary>
    /// Validates a URL for browser.open operations.
    /// </summary>
    public PolicyVerdict ValidateUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return PolicyVerdict.Deny("Invalid URL format");

        if (!_config.AllowedUrlSchemes.Contains(uri.Scheme))
            return PolicyVerdict.Deny($"URL scheme '{uri.Scheme}' is not allowed. Allowed: [{string.Join(", ", _config.AllowedUrlSchemes)}]");

        if (_config.BlockedUrlDomains.Contains(uri.Host))
            return PolicyVerdict.Deny($"Domain '{uri.Host}' is blocked");

        return PolicyVerdict.Allow();
    }

    // ── Process Validation ──────────────────────────────────────────────────────

    /// <summary>
    /// Validates a process name for launch or kill operations.
    /// </summary>
    public PolicyVerdict ValidateProcess(string processName, ProcessOperation operation)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return PolicyVerdict.Deny("Process name is empty");

        // Strip extension if present for consistent matching
        var baseName = Path.GetFileNameWithoutExtension(processName);

        if (operation == ProcessOperation.Kill)
        {
            if (_config.ProtectedProcesses.Contains(baseName))
                return PolicyVerdict.Deny($"Process '{baseName}' is a protected system process and cannot be killed");
        }

        if (operation == ProcessOperation.Launch)
        {
            if (_config.AllowedApplications.Count == 0)
                return PolicyVerdict.Deny("No applications are in the launch whitelist");

            if (!_config.AllowedApplications.Contains(baseName))
                return PolicyVerdict.Deny($"Application '{baseName}' is not in the launch whitelist");
        }

        return PolicyVerdict.Allow();
    }

    // ── Shell Command Validation ────────────────────────────────────────────────

    /// <summary>
    /// Validates a shell command for execution.
    /// </summary>
    public PolicyVerdict ValidateShellCommand(string command)
    {
        if (!_config.EnableShellCommands)
            return PolicyVerdict.Deny("Shell command execution is disabled");

        if (string.IsNullOrWhiteSpace(command))
            return PolicyVerdict.Deny("Command is empty");

        // Check against blocklist patterns (always applied first)
        foreach (var regex in _blockedShellRegexes.Value)
        {
            if (regex.IsMatch(command))
                return PolicyVerdict.Deny($"Command matches a blocked pattern");
        }

        // Check against allowlist
        if (_config.AllowedShellCommands.Count == 0)
            return PolicyVerdict.Deny("No shell commands are in the allowlist");

        bool commandAllowed = _config.AllowedShellCommands.Any(allowed =>
            command.StartsWith(allowed, StringComparison.OrdinalIgnoreCase));

        if (!commandAllowed)
            return PolicyVerdict.Deny("Command is not in the allowlist");

        return PolicyVerdict.Allow();
    }

    // ── Sensitive Data Scanning ─────────────────────────────────────────────────

    /// <summary>
    /// Scans outbound result content for sensitive data patterns.
    /// Reuses the same patterns as <see cref="OpenClawSecurityPolicy"/>.
    /// </summary>
    public PolicyVerdict ValidateOutboundContent(string content)
    {
        if (!_config.ScanOutboundResults || string.IsNullOrEmpty(content))
            return PolicyVerdict.Allow();

        var match = ScanForSensitiveData(content);
        if (match != null)
            return PolicyVerdict.Deny($"Result contains sensitive data: {match}");

        return PolicyVerdict.Allow();
    }

    /// <summary>
    /// Checks whether a capability at the given risk level requires user approval.
    /// </summary>
    public bool RequiresApproval(PcNodeRiskLevel riskLevel, bool handlerRequiresApproval)
    {
        return handlerRequiresApproval || riskLevel >= _config.ApprovalThreshold;
    }

    // ── Internals ───────────────────────────────────────────────────────────────

    private bool IsCallerAllowed(string callerDeviceId)
    {
        if (_config.AllowedCallerDeviceIds.Count == 0)
            return false;

        return _config.AllowedCallerDeviceIds.Contains("*") ||
               _config.AllowedCallerDeviceIds.Contains(callerDeviceId);
    }

    private (bool IsAllowed, string? Reason) CheckRateLimit(PcNodeRiskLevel riskLevel)
    {
        lock (_rateLimitLock)
        {
            var now = DateTime.UtcNow;
            var cutoff = now.AddMinutes(-1);

            // Global rate limit
            PruneQueue(_globalInvocations, cutoff);
            if (_globalInvocations.Count >= _config.GlobalRateLimitPerMinute)
            {
                return (false,
                    $"Global limit of {_config.GlobalRateLimitPerMinute} invocations per minute reached");
            }

            // High-risk rate limit
            if (riskLevel >= PcNodeRiskLevel.High)
            {
                PruneQueue(_highRiskInvocations, cutoff);
                if (_highRiskInvocations.Count >= _config.HighRiskRateLimitPerMinute)
                {
                    return (false,
                        $"High-risk limit of {_config.HighRiskRateLimitPerMinute} invocations per minute reached");
                }
            }

            return (true, null);
        }
    }

    private void RecordInvocation(PcNodeRiskLevel riskLevel)
    {
        lock (_rateLimitLock)
        {
            var now = DateTime.UtcNow;
            _globalInvocations.Enqueue(now);

            if (riskLevel >= PcNodeRiskLevel.High)
                _highRiskInvocations.Enqueue(now);
        }
    }

    private static void PruneQueue(Queue<DateTime> queue, DateTime cutoff)
    {
        while (queue.Count > 0 && queue.Peek() < cutoff)
            queue.Dequeue();
    }

    // Reuse the same sensitive data patterns as the outbound security policy
    private static readonly (Regex Pattern, string Label)[] SensitivePatterns =
    [
        (new Regex(@"(?:api[_-]?key|apikey|token|bearer|authorization)\s*[:=]\s*['""]?[A-Za-z0-9\-_.]{20,}",
            RegexOptions.IgnoreCase | RegexOptions.Compiled), "API key/token"),

        (new Regex(@"AKIA[0-9A-Z]{16}", RegexOptions.Compiled), "AWS access key"),

        (new Regex(@"-----BEGIN\s+(RSA\s+)?PRIVATE\s+KEY-----", RegexOptions.Compiled), "Private key"),

        (new Regex(@"(?:mongodb|postgres|mysql|redis|amqp)://\S+:(\S+)@",
            RegexOptions.IgnoreCase | RegexOptions.Compiled), "Connection string with password"),

        (new Regex(@"(?:password|passwd|pwd|secret)\s*[:=]\s*['""]?[^\s'""]{8,}",
            RegexOptions.IgnoreCase | RegexOptions.Compiled), "Password/secret"),

        (new Regex(@"eyJ[A-Za-z0-9_-]{10,}\.eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}",
            RegexOptions.Compiled), "JWT token"),

        (new Regex(@"\b(?:4[0-9]{12}(?:[0-9]{3})?|5[1-5][0-9]{14}|3[47][0-9]{13}|6(?:011|5[0-9]{2})[0-9]{12})\b",
            RegexOptions.Compiled), "Credit card number"),

        (new Regex(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled), "SSN-like pattern"),
    ];

    private static string? ScanForSensitiveData(string content)
    {
        foreach (var (pattern, label) in SensitivePatterns)
        {
            if (pattern.IsMatch(content))
                return label;
        }

        return null;
    }
}

/// <summary>
/// File operation type for path validation.
/// </summary>
public enum FileOperation
{
    Read,
    Write,
    List,
    Delete,
}

/// <summary>
/// Process operation type for process validation.
/// </summary>
public enum ProcessOperation
{
    Launch,
    Kill,
    List,
}
