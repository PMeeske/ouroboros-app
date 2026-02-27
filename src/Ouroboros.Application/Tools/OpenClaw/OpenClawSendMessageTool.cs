// <copyright file="OpenClawSendMessageTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Application.OpenClaw;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Send a message through any OpenClaw channel (WhatsApp, Telegram, Slack, Discord, Signal, iMessage, etc.).
/// </summary>
public sealed class OpenClawSendMessageTool : ITool
{
    public string Name => "openclaw_send_message";
    public string Description => "Send a message through any OpenClaw channel (WhatsApp, Telegram, Slack, Discord, Signal, iMessage, etc.).";
    public string? JsonSchema => """{"type":"object","properties":{"channel":{"type":"string","description":"Channel name (whatsapp, telegram, slack, discord, signal, imessage, etc.)"},"to":{"type":"string","description":"Recipient identifier (phone number, username, channel ID)"},"message":{"type":"string","description":"Message text to send"}},"required":["channel","to","message"]}""";

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (OpenClawSharedState.SharedClient == null || !OpenClawSharedState.SharedClient.IsConnected)
            return OpenClawSharedState.NotConnected();
        if (OpenClawSharedState.SharedPolicy == null)
            return OpenClawSharedState.PolicyNotInitialized();

        try
        {
            using var doc = JsonDocument.Parse(input);
            var root = doc.RootElement;

            var channel = root.GetProperty("channel").GetString() ?? "";
            var to = root.GetProperty("to").GetString() ?? "";
            var message = root.GetProperty("message").GetString() ?? "";

            // Security policy check (mandatory)
            var verdict = OpenClawSharedState.SharedPolicy.ValidateSendMessage(channel, to, message);
            if (!verdict.IsAllowed)
                return Result<string, string>.Failure($"Security policy denied: {verdict.Reason}");

            var result = await OpenClawSharedState.SharedClient.SendRequestAsync("chat.send", new
            {
                channel,
                to,
                message,
            }, ct);

            return Result<string, string>.Success($"Message sent via {channel} to {to}. Response: {result}");
        }
        catch (JsonException)
        {
            return Result<string, string>.Failure("Invalid JSON input. Expected: {\"channel\":\"...\",\"to\":\"...\",\"message\":\"...\"}");
        }
        catch (OpenClawException ex)
        {
            return Result<string, string>.Failure($"Gateway error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Failed to send message: {ex.Message}");
        }
    }
}
