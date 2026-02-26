// <copyright file="BrowserOpenHandler.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Text.Json;

namespace Ouroboros.Application.OpenClaw.PcNode.Handlers;

/// <summary>
/// Opens a URL in the default browser.
/// Validates the URL against the security policy (scheme allowlist, domain blocklist).
/// </summary>
public sealed class BrowserOpenHandler : IPcNodeCapabilityHandler
{
    private readonly PcNodeSecurityPolicy _policy;

    public BrowserOpenHandler(PcNodeSecurityPolicy policy)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public string CapabilityName => "browser.open";
    public string Description => "Open a URL in the default web browser";
    public PcNodeRiskLevel RiskLevel => PcNodeRiskLevel.Medium;

    public string? ParameterSchema => """
        {
          "type": "object",
          "properties": {
            "url": { "type": "string", "description": "URL to open" }
          },
          "required": ["url"]
        }
        """;

    public bool RequiresApproval => false;

    public Task<PcNodeResult> ExecuteAsync(
        JsonElement parameters, PcNodeExecutionContext context, CancellationToken ct)
    {
        var url = parameters.TryGetProperty("url", out var u) ? u.GetString() : null;
        if (string.IsNullOrWhiteSpace(url))
            return Task.FromResult(PcNodeResult.Fail("Missing required parameter 'url'"));

        var verdict = _policy.ValidateUrl(url);
        if (!verdict.IsAllowed)
            return Task.FromResult(PcNodeResult.Fail(verdict.Reason!));

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return Task.FromResult(PcNodeResult.Ok($"Opened URL: {url}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(PcNodeResult.Fail($"Failed to open URL: {ex.Message}"));
        }
    }
}
