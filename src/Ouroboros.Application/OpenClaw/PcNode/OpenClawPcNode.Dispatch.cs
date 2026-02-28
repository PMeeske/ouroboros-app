// <copyright file="OpenClawPcNode.Dispatch.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Ouroboros.Application.OpenClaw.PcNode;

/// <summary>
/// Message processing, node invoke dispatch, response sending, and approval handling.
/// </summary>
public sealed partial class OpenClawPcNode
{
    private async Task ProcessMessageAsync(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var type = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;
            var method = root.TryGetProperty("method", out var methodProp) ? methodProp.GetString() : null;
            var id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;

            // Handle node.invoke requests
            if (type == "req" && method == "node.invoke" && id != null)
            {
                await HandleNodeInvokeAsync(id, root);
                return;
            }

            // Handle events
            if (type == "event")
            {
                var eventType = root.TryGetProperty("event", out var ev) ? ev.GetString() ?? "unknown" : "unknown";
                var payload = root.TryGetProperty("data", out var data) ? data.Clone() : root.Clone();
                OnEvent?.Invoke(new OpenClawEvent(eventType, payload, DateTime.UtcNow));
                return;
            }

            _logger.LogDebug("[OpenClaw PC Node] Unhandled message: {Type}/{Method}",
                type ?? "?", method ?? "?");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("[OpenClaw PC Node] Failed to parse message: {Message}", ex.Message);
        }
    }

    private async Task HandleNodeInvokeAsync(string requestId, JsonElement root)
    {
        string? capability = null;
        string callerDeviceId = "unknown";

        try
        {
            var @params = root.TryGetProperty("params", out var p) ? p : default;
            capability = @params.TryGetProperty("command", out var cmdEl) ? cmdEl.GetString() : null;
            callerDeviceId = @params.TryGetProperty("callerDeviceId", out var callerEl)
                ? callerEl.GetString() ?? "unknown" : "unknown";
            var invokeParams = @params.TryGetProperty("params", out var ip) ? ip : default;

            if (string.IsNullOrEmpty(capability))
            {
                await SendResponseAsync(requestId, false, null, "Missing 'command' in node.invoke params");
                return;
            }

            // Get handler
            var handler = _capabilities.GetHandler(capability);
            if (handler == null)
            {
                await SendResponseAsync(requestId, false, null, $"Unknown capability: {capability}");
                return;
            }

            // Security policy check
            var verdict = _security.ValidateIncomingInvoke(
                callerDeviceId, capability, invokeParams.ValueKind != JsonValueKind.Undefined ? invokeParams : null,
                handler.RiskLevel);

            if (!verdict.IsAllowed)
            {
                await SendResponseAsync(requestId, false, null, $"Security policy denied: {verdict.Reason}");
                return;
            }

            // Approval check
            if (_security.RequiresApproval(handler.RiskLevel, handler.RequiresApproval))
            {
                var approved = await RequestApprovalAsync(new ApprovalRequest(
                    requestId, callerDeviceId, capability,
                    invokeParams.ToString(), handler.RiskLevel));

                if (!approved)
                {
                    _auditLog.LogDenied("pc_node_invoke", capability, callerDeviceId, "User denied approval");
                    await SendResponseAsync(requestId, false, null, "Operation denied by user");
                    return;
                }
            }

            // Execute
            var context = new PcNodeExecutionContext(requestId, callerDeviceId, DateTime.UtcNow, _auditLog);
            var result = await handler.ExecuteAsync(invokeParams, context, CancellationToken.None);

            if (result.Success)
            {
                // Scan outbound content for sensitive data
                var resultText = result.Data?.ToString() ?? "";
                var contentVerdict = _security.ValidateOutboundContent(resultText);
                if (!contentVerdict.IsAllowed)
                {
                    _auditLog.LogDenied("pc_node_result", capability, callerDeviceId,
                        $"Outbound content blocked: {contentVerdict.Reason}");
                    await SendResponseAsync(requestId, false, null,
                        $"Result blocked by security policy: {contentVerdict.Reason}");
                    return;
                }

                await SendResponseAsync(requestId, true, result.Data, null, result.Base64Payload);
            }
            else
            {
                await SendResponseAsync(requestId, false, null, result.Error);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (System.Net.WebSockets.WebSocketException ex)
        {
            _logger.LogError(ex, "[OpenClaw PC Node] Error handling invoke for {Capability}", capability ?? "?");
            await SendResponseAsync(requestId, false, null, $"Internal error: {ex.Message}");
        }
    }

    private async Task SendResponseAsync(
        string requestId,
        bool success,
        JsonElement? data,
        string? error,
        string? base64Payload = null)
    {
        var response = new Dictionary<string, object?>
        {
            ["type"] = "res",
            ["id"] = requestId,
            ["ok"] = success,
        };

        if (success)
        {
            var resultObj = new Dictionary<string, object?>();
            if (data.HasValue)
                resultObj["data"] = data.Value;
            if (base64Payload != null)
                resultObj["payload"] = base64Payload;
            response["result"] = resultObj;
        }
        else
        {
            response["error"] = new { message = error ?? "Unknown error" };
        }

        var json = JsonSerializer.Serialize(response);
        var bytes = Encoding.UTF8.GetBytes(json);

        await _sendLock.WaitAsync();
        try
        {
            if (_ws.State == WebSocketState.Open)
                await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task<bool> RequestApprovalAsync(ApprovalRequest request)
    {
        if (OnApprovalRequired == null)
        {
            _logger.LogWarning("[OpenClaw PC Node] No approval handler registered; denying {Capability}",
                request.Capability);
            return false;
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.ApprovalTimeoutSeconds));
            var approvalTask = OnApprovalRequired(request);
            var completed = await Task.WhenAny(approvalTask, Task.Delay(-1, cts.Token));
            return completed == approvalTask && await approvalTask;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[OpenClaw PC Node] Approval timed out for {Capability}", request.Capability);
            return false;
        }
    }

    private async Task<string> ReadFullMessageAsync(CancellationToken ct)
    {
        byte[] buffer = new byte[8192];
        StringBuilder accumulated = new();

        while (true)
        {
            var result = await _ws.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close)
                throw new WebSocketException("Gateway closed connection during handshake");

            accumulated.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

            if (result.EndOfMessage)
                return accumulated.ToString();
        }
    }
}
