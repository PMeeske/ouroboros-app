// <copyright file="ApiKeyAuthMiddleware.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Ouroboros.ApiHost.Middleware;

/// <summary>
/// Middleware that enforces API key authentication via the <c>X-Api-Key</c> header.
/// Skips authentication for health probes, Swagger, and the root discovery endpoint.
/// When no API key is configured (e.g., local dev), authentication is bypassed.
/// </summary>
public sealed class ApiKeyAuthMiddleware
{
    private const string ApiKeyHeaderName = "X-Api-Key";
    private const string ConfigKeyName = "ApiKey";

    private static readonly HashSet<string> SkipPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/",
        "/health",
        "/ready",
        "/swagger",
    };

    private readonly RequestDelegate _next;
    private readonly string? _configuredApiKey;
    private readonly ILogger<ApiKeyAuthMiddleware> _logger;

    public ApiKeyAuthMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<ApiKeyAuthMiddleware> logger)
    {
        _next = next;
        _configuredApiKey = configuration[ConfigKeyName];
        _logger = logger;

        if (string.IsNullOrEmpty(_configuredApiKey))
        {
            _logger.LogWarning(
                "No API key configured (set '{ConfigKey}' in appsettings or environment). " +
                "API key authentication is DISABLED — all requests will be allowed.",
                ConfigKeyName);
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip auth for health probes, swagger, and root
        var path = context.Request.Path.Value ?? "/";
        if (ShouldSkip(path))
        {
            await _next(context);
            return;
        }

        // If no key is configured, bypass auth (local dev)
        if (string.IsNullOrEmpty(_configuredApiKey))
        {
            await _next(context);
            return;
        }

        // Check for API key header
        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedKey) ||
            string.IsNullOrEmpty(providedKey))
        {
            _logger.LogWarning("Request to {Path} rejected: missing {Header} header",
                path, ApiKeyHeaderName);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc7235#section-3.1",
                title = "Unauthorized",
                status = 401,
                detail = $"Missing or empty '{ApiKeyHeaderName}' header."
            });
            return;
        }

        // Constant-time comparison to prevent timing attacks
        if (!CryptographicEquals(_configuredApiKey, providedKey!))
        {
            _logger.LogWarning("Request to {Path} rejected: invalid API key", path);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                title = "Forbidden",
                status = 403,
                detail = "Invalid API key."
            });
            return;
        }

        await _next(context);
    }

    private static bool ShouldSkip(string path)
    {
        if (SkipPaths.Contains(path))
            return true;

        // Also skip swagger sub-paths
        if (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static bool CryptographicEquals(string a, string b)
    {
        if (a.Length != b.Length)
            return false;

        int result = 0;
        for (int i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }

        return result == 0;
    }
}
