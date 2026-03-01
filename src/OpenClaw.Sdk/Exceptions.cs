// <copyright file="Exceptions.cs" company="OpenClaw.Sdk">
// .NET port of the OpenClaw Python SDK (github.com/masteryodaa/openclaw-sdk)
// </copyright>

namespace OpenClaw.Sdk;

/// <summary>Base exception for all OpenClaw SDK errors.</summary>
public class OpenClawException : Exception
{
    public string? Code { get; }
    public Dictionary<string, object> Details { get; }
    public int? StatusCode { get; }
    public float? RetryAfter { get; }
    public virtual bool IsRetryable => false;

    public OpenClawException(string message, string? code = null,
        Dictionary<string, object>? details = null,
        int? statusCode = null, float? retryAfter = null)
        : base(message)
    {
        Code = code;
        Details = details ?? [];
        StatusCode = statusCode;
        RetryAfter = retryAfter;
    }
}

public sealed class ConfigurationException(string message) : OpenClawException(message);
public sealed class GatewayException(string message, string? code = null) : OpenClawException(message, code);
public sealed class AgentNotFoundException(string agentId) : OpenClawException($"Agent '{agentId}' not found.");
public sealed class AgentExecutionException(string message) : OpenClawException(message);
public sealed class GatewayTimeoutException(string message) : OpenClawException(message) { public override bool IsRetryable => true; }
public sealed class StreamException(string message) : OpenClawException(message);
public sealed class AuthenticationException(string message) : OpenClawException(message) { public override bool IsRetryable => false; }

public sealed class RateLimitException(string message, float? retryAfter = null)
    : OpenClawException(message, retryAfter: retryAfter) { public override bool IsRetryable => true; }
