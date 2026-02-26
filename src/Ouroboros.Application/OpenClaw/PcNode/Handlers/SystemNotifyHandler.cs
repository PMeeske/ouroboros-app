// <copyright file="SystemNotifyHandler.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Text.Json;

namespace Ouroboros.Application.OpenClaw.PcNode.Handlers;

/// <summary>
/// Shows a desktop notification (Windows balloon tip via PowerShell).
/// </summary>
public sealed class SystemNotifyHandler : IPcNodeCapabilityHandler
{
    public string CapabilityName => "system.notify";
    public string Description => "Show a desktop notification with a title and message";
    public PcNodeRiskLevel RiskLevel => PcNodeRiskLevel.Low;

    public string? ParameterSchema => """
        {
          "type": "object",
          "properties": {
            "title":   { "type": "string", "description": "Notification title (default: Ouroboros)" },
            "message": { "type": "string", "description": "Notification body text" }
          },
          "required": ["message"]
        }
        """;

    public bool RequiresApproval => false;

    public Task<PcNodeResult> ExecuteAsync(
        JsonElement parameters, PcNodeExecutionContext context, CancellationToken ct)
    {
        var title = parameters.TryGetProperty("title", out var t)
            ? t.GetString() ?? "Ouroboros"
            : "Ouroboros";

        var message = parameters.TryGetProperty("message", out var m) ? m.GetString() : null;
        if (string.IsNullOrWhiteSpace(message))
            return Task.FromResult(PcNodeResult.Fail("Missing required parameter 'message'"));

        if (!OperatingSystem.IsWindows())
            return Task.FromResult(PcNodeResult.Fail("Desktop notifications are only supported on Windows"));

        try
        {
            var escapedTitle = title.Replace("'", "''");
            var escapedMessage = message.Replace("'", "''");

            var script =
                $"[void][System.Reflection.Assembly]::LoadWithPartialName('System.Windows.Forms');" +
                $"$n=New-Object System.Windows.Forms.NotifyIcon;" +
                $"$n.Icon=[System.Drawing.SystemIcons]::Information;" +
                $"$n.BalloonTipTitle='{escapedTitle}';" +
                $"$n.BalloonTipText='{escapedMessage}';" +
                $"$n.Visible=$true;" +
                $"$n.ShowBalloonTip(5000);" +
                $"Start-Sleep -Seconds 6;" +
                $"$n.Dispose()";

            Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -WindowStyle Hidden -Command \"{script}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            });

            return Task.FromResult(PcNodeResult.Ok($"Notification shown: {title}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(PcNodeResult.Fail($"Failed to show notification: {ex.Message}"));
        }
    }
}
