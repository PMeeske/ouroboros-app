// <copyright file="OpenClawSecurityPolicy.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ouroboros.Application.OpenClaw;

/// <summary>
/// Security policy for OpenClaw Gateway integration.
///
/// Enforces multiple layers of protection before any message or node command
/// reaches the Gateway:
///   1. Channel allowlist — only configured channels may be used
///   2. Recipient allowlist — opt-in per-channel recipient restrictions
///   3. Node command allowlist — restricts which device commands are invocable
///   4. Sensitive data redaction — blocks API keys, tokens, passwords, PII patterns
///   5. Rate limiting — per-channel and global message throttling
///   6. Content length limits — prevents oversized payloads
///
/// Default stance: deny unless explicitly allowed. Fail-closed on all errors.
/// </summary>
public sealed partial class OpenClawSecurityPolicy
{
    private readonly OpenClawSecurityConfig _config;
    private readonly OpenClawAuditLog _auditLog;

    // Rate-limiting state: channel → (timestamps of recent sends)
    private readonly Dictionary<string, Queue<DateTime>> _channelSendTimes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<DateTime> _globalSendTimes = new();
    private readonly object _rateLimitLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenClawSecurityPolicy"/> class.
    /// </summary>
    public OpenClawSecurityPolicy(OpenClawSecurityConfig config, OpenClawAuditLog auditLog)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _auditLog = auditLog ?? throw new ArgumentNullException(nameof(auditLog));
    }

    // ── Message Validation ──────────────────────────────────────────────────────

    /// <summary>
    /// Validates a send-message request against all policy layers.
    /// Returns a <see cref="PolicyVerdict"/> with Allow/Deny and reason.
    /// </summary>
    public PolicyVerdict ValidateSendMessage(string channel, string recipient, string message)
    {
        // 1. Channel allowlist
        if (!IsChannelAllowed(channel))
        {
            var verdict = PolicyVerdict.Deny($"Channel '{channel}' is not in the allowlist. Allowed: [{string.Join(", ", _config.AllowedChannels)}]");
            _auditLog.LogDenied("openclaw_send_message", channel, recipient, verdict.Reason);
            return verdict;
        }

        // 2. Recipient allowlist (if configured for this channel)
        if (!IsRecipientAllowed(channel, recipient))
        {
            var verdict = PolicyVerdict.Deny($"Recipient '{MaskRecipient(recipient)}' is not in the allowlist for channel '{channel}'");
            _auditLog.LogDenied("openclaw_send_message", channel, recipient, verdict.Reason);
            return verdict;
        }

        // 3. Content length check
        if (message.Length > _config.MaxMessageLength)
        {
            var verdict = PolicyVerdict.Deny($"Message length ({message.Length}) exceeds maximum ({_config.MaxMessageLength})");
            _auditLog.LogDenied("openclaw_send_message", channel, recipient, verdict.Reason);
            return verdict;
        }

        // 4. Sensitive data scan
        var sensitiveMatch = ScanForSensitiveData(message);
        if (sensitiveMatch != null)
        {
            var verdict = PolicyVerdict.Deny($"Message contains potentially sensitive data: {sensitiveMatch}. Use redaction or remove before sending.");
            _auditLog.LogDenied("openclaw_send_message", channel, recipient, verdict.Reason);
            return verdict;
        }

        // 5. Rate limiting
        var rateLimitResult = CheckRateLimit(channel);
        if (!rateLimitResult.IsAllowed)
        {
            var verdict = PolicyVerdict.Deny($"Rate limit exceeded: {rateLimitResult.Reason}");
            _auditLog.LogDenied("openclaw_send_message", channel, recipient, verdict.Reason);
            return verdict;
        }

        // All checks passed
        _auditLog.LogAllowed("openclaw_send_message", channel, recipient, $"message length={message.Length}");
        RecordSend(channel);
        return PolicyVerdict.Allow();
    }

    // ── Node Invoke Validation ──────────────────────────────────────────────────

    /// <summary>
    /// Validates a node invoke request against the command allowlist and rate limits.
    /// </summary>
    public PolicyVerdict ValidateNodeInvoke(string nodeId, string command, string? paramsJson)
    {
        // 1. Command allowlist
        if (!IsNodeCommandAllowed(command))
        {
            var verdict = PolicyVerdict.Deny($"Node command '{command}' is not in the allowlist. Allowed: [{string.Join(", ", _config.AllowedNodeCommands)}]");
            _auditLog.LogDenied("openclaw_node_invoke", nodeId, command, verdict.Reason);
            return verdict;
        }

        // 2. Dangerous command double-check
        if (IsDangerousCommand(command))
        {
            var verdict = PolicyVerdict.Deny($"Node command '{command}' is classified as dangerous and requires explicit override");
            _auditLog.LogDenied("openclaw_node_invoke", nodeId, command, verdict.Reason);
            return verdict;
        }

        // 3. Validate params if present (e.g., sms.send should check recipient)
        if (command.Equals("sms.send", StringComparison.OrdinalIgnoreCase) && paramsJson != null)
        {
            var smsVerdict = ValidateSmsParams(paramsJson);
            if (!smsVerdict.IsAllowed)
            {
                _auditLog.LogDenied("openclaw_node_invoke", nodeId, command, smsVerdict.Reason);
                return smsVerdict;
            }
        }

        // 4. Rate limiting (node invocations use global rate limit)
        var rateLimitResult = CheckRateLimit("__node_invoke__");
        if (!rateLimitResult.IsAllowed)
        {
            var verdict = PolicyVerdict.Deny($"Rate limit exceeded for node invocations: {rateLimitResult.Reason}");
            _auditLog.LogDenied("openclaw_node_invoke", nodeId, command, verdict.Reason);
            return verdict;
        }

        _auditLog.LogAllowed("openclaw_node_invoke", nodeId, command, paramsJson ?? "{}");
        RecordSend("__node_invoke__");
        return PolicyVerdict.Allow();
    }

    // ── Channel Allowlist ───────────────────────────────────────────────────────

    private bool IsChannelAllowed(string channel)
    {
        // If no allowlist configured, deny all (fail-closed)
        if (_config.AllowedChannels.Count == 0)
            return false;

        return _config.AllowedChannels.Contains(channel);
    }

    // ── Recipient Allowlist ─────────────────────────────────────────────────────

    private bool IsRecipientAllowed(string channel, string recipient)
    {
        // If no per-channel recipient restrictions, allow all recipients on allowed channels
        if (!_config.AllowedRecipients.TryGetValue(channel, out var allowedRecipients))
            return true;

        // If the channel has a recipient list, enforce it
        if (allowedRecipients.Count == 0)
            return true;

        return allowedRecipients.Contains(recipient);
    }

    // ── Node Command Allowlist ──────────────────────────────────────────────────

    private bool IsNodeCommandAllowed(string command)
    {
        if (_config.AllowedNodeCommands.Count == 0)
            return false;

        // Match exact command or wildcard prefix (e.g., "camera.*")
        foreach (var allowed in _config.AllowedNodeCommands)
        {
            if (allowed.EndsWith(".*", StringComparison.Ordinal))
            {
                var prefix = allowed[..^2]; // Strip ".*"
                if (command.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase) ||
                    command.Equals(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else if (string.Equals(allowed, command, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDangerousCommand(string command)
    {
        // system.run is inherently dangerous — can execute arbitrary shell commands
        return command.Equals("system.run", StringComparison.OrdinalIgnoreCase);
    }

    // ── Sensitive Data Detection ────────────────────────────────────────────────

    /// <summary>
    /// Scans content for sensitive data using source-generated regex patterns.
    /// </summary>
    private static string? ScanForSensitiveData(string content)
    {
        if (ApiKeyTokenRegex().IsMatch(content)) return "API key/token";
        if (AwsAccessKeyRegex().IsMatch(content)) return "AWS access key";
        if (PrivateKeyRegex().IsMatch(content)) return "Private key";
        if (ConnectionStringRegex().IsMatch(content)) return "Connection string with password";
        if (PasswordSecretRegex().IsMatch(content)) return "Password/secret";
        if (JwtTokenRegex().IsMatch(content)) return "JWT token";
        if (CreditCardRegex().IsMatch(content)) return "Credit card number";
        if (SsnRegex().IsMatch(content)) return "SSN-like pattern";

        return null;
    }

    [GeneratedRegex(@"(?:api[_-]?key|apikey|token|bearer|authorization)\s*[:=]\s*['""]?[A-Za-z0-9\-_.]{20,}", RegexOptions.IgnoreCase)]
    private static partial Regex ApiKeyTokenRegex();

    [GeneratedRegex(@"AKIA[0-9A-Z]{16}")]
    private static partial Regex AwsAccessKeyRegex();

    [GeneratedRegex(@"-----BEGIN\s+(RSA\s+)?PRIVATE\s+KEY-----")]
    private static partial Regex PrivateKeyRegex();

    [GeneratedRegex(@"(?:mongodb|postgres|mysql|redis|amqp)://\S+:(\S+)@", RegexOptions.IgnoreCase)]
    private static partial Regex ConnectionStringRegex();

    [GeneratedRegex(@"(?:password|passwd|pwd|secret)\s*[:=]\s*['""]?[^\s'""]{8,}", RegexOptions.IgnoreCase)]
    private static partial Regex PasswordSecretRegex();

    [GeneratedRegex(@"eyJ[A-Za-z0-9_-]{10,}\.eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}")]
    private static partial Regex JwtTokenRegex();

    [GeneratedRegex(@"\b(?:4[0-9]{12}(?:[0-9]{3})?|5[1-5][0-9]{14}|3[47][0-9]{13}|6(?:011|5[0-9]{2})[0-9]{12})\b")]
    private static partial Regex CreditCardRegex();

    [GeneratedRegex(@"\b\d{3}-\d{2}-\d{4}\b")]
    private static partial Regex SsnRegex();

    // ── SMS Parameter Validation ────────────────────────────────────────────────

    private PolicyVerdict ValidateSmsParams(string paramsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(paramsJson);
            if (doc.RootElement.TryGetProperty("to", out var toElement))
            {
                var to = toElement.GetString();
                if (string.IsNullOrWhiteSpace(to))
                    return PolicyVerdict.Deny("sms.send: 'to' field is empty");

                // Check SMS recipient against allowed recipients
                if (_config.AllowedRecipients.TryGetValue("sms", out var allowedSms) && allowedSms.Count > 0)
                {
                    if (!allowedSms.Contains(to))
                        return PolicyVerdict.Deny($"sms.send: recipient '{MaskRecipient(to)}' is not in the SMS allowlist");
                }
            }

            // Check message content for sensitive data
            if (doc.RootElement.TryGetProperty("message", out var msgElement))
            {
                var msg = msgElement.GetString();
                if (msg != null)
                {
                    var sensitiveMatch = ScanForSensitiveData(msg);
                    if (sensitiveMatch != null)
                        return PolicyVerdict.Deny($"sms.send: message contains sensitive data ({sensitiveMatch})");
                }
            }
        }
        catch (JsonException)
        {
            return PolicyVerdict.Deny("sms.send: invalid JSON parameters");
        }

        return PolicyVerdict.Allow();
    }

    // ── Rate Limiting ───────────────────────────────────────────────────────────

    private RateLimitResult CheckRateLimit(string channel)
    {
        lock (_rateLimitLock)
        {
            var now = DateTime.UtcNow;

            // Global rate limit
            PruneQueue(_globalSendTimes, now, _config.RateLimitWindowSeconds);
            if (_globalSendTimes.Count >= _config.GlobalRateLimitPerWindow)
            {
                return new RateLimitResult(false,
                    $"Global limit of {_config.GlobalRateLimitPerWindow} messages per {_config.RateLimitWindowSeconds}s reached");
            }

            // Per-channel rate limit
            if (!_channelSendTimes.TryGetValue(channel, out var channelQueue))
            {
                channelQueue = new Queue<DateTime>();
                _channelSendTimes[channel] = channelQueue;
            }

            PruneQueue(channelQueue, now, _config.RateLimitWindowSeconds);
            if (channelQueue.Count >= _config.ChannelRateLimitPerWindow)
            {
                return new RateLimitResult(false,
                    $"Channel '{channel}' limit of {_config.ChannelRateLimitPerWindow} per {_config.RateLimitWindowSeconds}s reached");
            }

            return new RateLimitResult(true, null);
        }
    }

    private void RecordSend(string channel)
    {
        lock (_rateLimitLock)
        {
            var now = DateTime.UtcNow;
            _globalSendTimes.Enqueue(now);

            if (!_channelSendTimes.TryGetValue(channel, out var channelQueue))
            {
                channelQueue = new Queue<DateTime>();
                _channelSendTimes[channel] = channelQueue;
            }

            channelQueue.Enqueue(now);
        }
    }

    private static void PruneQueue(Queue<DateTime> queue, DateTime now, int windowSeconds)
    {
        var cutoff = now.AddSeconds(-windowSeconds);
        while (queue.Count > 0 && queue.Peek() < cutoff)
            queue.Dequeue();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Masks a recipient identifier for safe logging (e.g., "+1555****0123").
    /// </summary>
    private static string MaskRecipient(string recipient)
    {
        if (string.IsNullOrEmpty(recipient) || recipient.Length < 6)
            return "***";

        // Phone numbers: show first 4 and last 4
        if (recipient.StartsWith('+') && recipient.Length >= 10)
            return recipient[..4] + new string('*', recipient.Length - 8) + recipient[^4..];

        // Email: mask local part
        var atIndex = recipient.IndexOf('@');
        if (atIndex > 0)
            return recipient[..1] + "***" + recipient[atIndex..];

        // Generic: show first 2 and last 2
        return recipient[..2] + "***" + recipient[^2..];
    }

    private readonly record struct RateLimitResult(bool IsAllowed, string? Reason);
}

/// <summary>
/// Result of a security policy evaluation. Fail-closed: default is Deny.
/// </summary>
public readonly record struct PolicyVerdict
{
    /// <summary>Whether the action is allowed.</summary>
    public bool IsAllowed { get; init; }

    /// <summary>Human-readable reason for denial (null when allowed).</summary>
    public string Reason { get; init; }

    /// <summary>Creates an Allow verdict.</summary>
    public static PolicyVerdict Allow() => new() { IsAllowed = true, Reason = string.Empty };

    /// <summary>Creates a Deny verdict with a reason.</summary>
    public static PolicyVerdict Deny(string reason) => new() { IsAllowed = false, Reason = reason };
}
