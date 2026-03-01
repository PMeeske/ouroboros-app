// <copyright file="ClientConfig.cs" company="OpenClaw.Sdk">
// .NET port of the OpenClaw Python SDK (github.com/masteryodaa/openclaw-sdk)
// </copyright>

namespace OpenClaw.Sdk.Config;

/// <summary>Configuration for the OpenClaw client.</summary>
public sealed class ClientConfig
{
    public GatewayMode Mode { get; init; } = GatewayMode.Auto;
    public string? GatewayWsUrl { get; init; }
    public string? ApiKey { get; init; }
    public int TimeoutSeconds { get; init; } = 300;
    public int MaxRetries { get; init; } = 3;
    public string LogLevel { get; init; } = "INFO";

    /// <summary>Build from <c>OPENCLAW_*</c> environment variables.</summary>
    public static ClientConfig FromEnv()
    {
        var url = Environment.GetEnvironmentVariable("OPENCLAW_GATEWAY_URL")
               ?? Environment.GetEnvironmentVariable("OPENCLAW_GATEWAY_WS_URL");
        var key = Environment.GetEnvironmentVariable("OPENCLAW_API_KEY");
        var modeStr = Environment.GetEnvironmentVariable("OPENCLAW_MODE");
        var timeoutStr = Environment.GetEnvironmentVariable("OPENCLAW_TIMEOUT");

        var mode = modeStr?.ToLowerInvariant() switch
        {
            "local"        => GatewayMode.Local,
            "protocol"     => GatewayMode.Protocol,
            "openai_compat"=> GatewayMode.OpenAiCompat,
            _              => GatewayMode.Auto,
        };

        return new ClientConfig
        {
            GatewayWsUrl   = url,
            ApiKey         = key,
            Mode           = mode,
            TimeoutSeconds = int.TryParse(timeoutStr, out var t) ? t : 300,
        };
    }
}

/// <summary>Options controlling a single agent execution.</summary>
public sealed class ExecutionOptions
{
    public int TimeoutSeconds { get; init; } = 300;
    public bool Stream { get; init; } = false;
    public int MaxToolCalls { get; init; } = 50;
    public string? Thinking { get; init; }
    public bool? Deliver { get; init; }
}
