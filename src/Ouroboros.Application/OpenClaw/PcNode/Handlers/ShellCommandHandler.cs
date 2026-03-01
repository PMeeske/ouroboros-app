// <copyright file="ShellCommandHandler.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.Tools;
using Ouroboros.Application.Tools.SystemTools;

namespace Ouroboros.Application.OpenClaw.PcNode.Handlers;

/// <summary>
/// Executes a shell command.
/// Delegates to <see cref="SystemAccessTools.PowerShellTool"/>.
/// <para>
/// <b>Disabled by default.</b> Requires <see cref="PcNodeSecurityConfig.EnableShellCommands"/> = true.
/// Commands are validated against both the allowlist and blocklist.
/// Output is capped at <see cref="PcNodeSecurityConfig.ShellOutputMaxBytes"/>.
/// Execution is capped at <see cref="PcNodeSecurityConfig.ShellCommandTimeoutSeconds"/>.
/// </para>
/// </summary>
public sealed class ShellCommandHandler : IPcNodeCapabilityHandler
{
    private readonly PcNodeSecurityPolicy _policy;
    private readonly PcNodeSecurityConfig _config;

    public ShellCommandHandler(PcNodeSecurityPolicy policy, PcNodeSecurityConfig config)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public string CapabilityName => "system.run";
    public string Description => "Execute a shell command (disabled by default, requires allowlist)";
    public PcNodeRiskLevel RiskLevel => PcNodeRiskLevel.Critical;

    public string? ParameterSchema => """
        {
          "type": "object",
          "properties": {
            "command": { "type": "string", "description": "Shell command to execute" }
          },
          "required": ["command"]
        }
        """;

    public bool RequiresApproval => true;

    public async Task<PcNodeResult> ExecuteAsync(
        JsonElement parameters, PcNodeExecutionContext context, CancellationToken ct)
    {
        var command = parameters.TryGetProperty("command", out var c) ? c.GetString() : null;
        if (string.IsNullOrWhiteSpace(command))
            return PcNodeResult.Fail("Missing required parameter 'command'");

        var verdict = _policy.ValidateShellCommand(command);
        if (!verdict.IsAllowed)
            return PcNodeResult.Fail(verdict.Reason!);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_config.ShellCommandTimeoutSeconds));

        try
        {
            var tool = new PowerShellTool();
            var result = await tool.InvokeAsync(command, cts.Token);

            if (!result.IsSuccess)
                return PcNodeResult.Fail(result.Error);

            var output = result.Value;

            // Truncate output if it exceeds the maximum
            if (output.Length > _config.ShellOutputMaxBytes)
            {
                output = output[..(int)_config.ShellOutputMaxBytes] +
                         $"\n... [truncated at {_config.ShellOutputMaxBytes:N0} bytes]";
            }

            return PcNodeResult.Ok(output);
        }
        catch (OperationCanceledException)
        {
            return PcNodeResult.Fail(
                $"Command timed out after {_config.ShellCommandTimeoutSeconds} seconds");
        }
    }
}
