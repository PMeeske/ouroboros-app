// <copyright file="OpenClawTokenManager.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Ouroboros.Application.OpenClaw;

/// <summary>
/// Resolves the OpenClaw gateway token from multiple sources with a priority chain:
///   1. Explicit value (CLI argument)
///   2. Environment variable <c>OPENCLAW_TOKEN</c>
///   3. .NET user-secrets / appsettings via IConfiguration
///   4. OpenClaw config file (<c>~/.openclaw/config</c> or WSL equivalent)
///
/// This follows the principle of least surprise — explicit always wins,
/// and the file-based fallback enables zero-config development setups.
/// </summary>
public static class OpenClawTokenManager
{
    private const string EnvVarName = "OPENCLAW_TOKEN";
    private const string UserSecretsKey = "OpenClaw:Token";

    /// <summary>
    /// Resolves the gateway token using the priority chain.
    /// Returns null if no token could be found (fail-closed: gateway will reject).
    /// </summary>
    /// <param name="explicitToken">Token from CLI argument (highest priority).</param>
    /// <param name="configuration">IConfiguration instance for user-secrets and appsettings lookup.</param>
    public static string? ResolveToken(string? explicitToken = null, IConfiguration? configuration = null)
    {
        // 1. Explicit value
        if (!string.IsNullOrWhiteSpace(explicitToken))
            return explicitToken;

        // 2. Environment variable
        var envToken = Environment.GetEnvironmentVariable(EnvVarName);
        if (!string.IsNullOrWhiteSpace(envToken))
            return envToken;

        // 3. .NET user-secrets / appsettings via IConfiguration
        if (configuration != null)
        {
            var configToken = configuration[UserSecretsKey];
            if (!string.IsNullOrWhiteSpace(configToken))
                return configToken;
        }

        // 4. OpenClaw config file
        var fileToken = ReadFromOpenClawConfig();
        if (!string.IsNullOrWhiteSpace(fileToken))
            return fileToken;

        return null;
    }

    /// <summary>
    /// Resolves the gateway URL using the priority chain.
    /// </summary>
    /// <param name="explicitUrl">URL from CLI argument (highest priority).</param>
    /// <param name="configuration">IConfiguration instance for user-secrets and appsettings lookup.</param>
    public static string ResolveGatewayUrl(string? explicitUrl = null, IConfiguration? configuration = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitUrl))
            return explicitUrl;

        var envUrl = Environment.GetEnvironmentVariable("OPENCLAW_GATEWAY");
        if (!string.IsNullOrWhiteSpace(envUrl))
            return envUrl;

        if (configuration != null)
        {
            var configUrl = configuration["OpenClaw:Gateway"];
            if (!string.IsNullOrWhiteSpace(configUrl))
                return configUrl;
        }

        return Configuration.DefaultEndpoints.OpenClawGateway;
    }

    /// <summary>
    /// Attempts to read the gateway auth token from the OpenClaw config file.
    /// Searches in order: ~/.openclaw/openclaw.json, ~/.openclaw/config, $APPDATA/openclaw/config.
    /// </summary>
    private static string? ReadFromOpenClawConfig()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var candidates = new[]
        {
            // Primary: npm-installed openclaw writes here on all platforms
            Path.Combine(home, ".openclaw", "openclaw.json"),
            // Legacy / alternative paths
            Path.Combine(home, ".openclaw", "config"),
            Path.Combine(appData, "openclaw", "config"),
            Path.Combine(appData, ".openclaw", "openclaw.json"),
        };

        foreach (var path in candidates)
        {
            if (!File.Exists(path))
                continue;

            try
            {
                var content = File.ReadAllText(path);

                // Try JSON format first
                if (content.TrimStart().StartsWith('{'))
                {
                    using var doc = JsonDocument.Parse(content);
                    if (doc.RootElement.TryGetProperty("gateway", out var gw)
                        && gw.TryGetProperty("auth", out var auth)
                        && auth.TryGetProperty("token", out var token))
                    {
                        return token.GetString();
                    }
                }

                // Try TOML-like key=value format
                foreach (var line in content.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("gateway.auth.token", StringComparison.OrdinalIgnoreCase))
                    {
                        var eqIdx = trimmed.IndexOf('=');
                        if (eqIdx > 0)
                        {
                            var value = trimmed[(eqIdx + 1)..].Trim().Trim('"', '\'');
                            if (!string.IsNullOrWhiteSpace(value))
                                return value;
                        }
                    }
                }
            }
            catch
            {
                // Fail silently — config file is optional
            }
        }

        return null;
    }
}
