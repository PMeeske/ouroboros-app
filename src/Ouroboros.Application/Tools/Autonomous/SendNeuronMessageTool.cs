// <copyright file="SendNeuronMessageTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Sends a message to a neuron.
/// </summary>
public class SendNeuronMessageTool : ITool
{
    private readonly IAutonomousToolContext _ctx;
    public SendNeuronMessageTool(IAutonomousToolContext context) => _ctx = context;
    public SendNeuronMessageTool() : this(AutonomousTools.DefaultContext) { }

    /// <inheritdoc/>
    public string Name => "send_to_neuron";

    /// <inheritdoc/>
    public string Description => """
        Send a message to one of my internal neurons. Input JSON:
        {
            "neuron_id": "neuron.memory|neuron.code|neuron.symbolic|neuron.executive|...",
            "topic": "message.topic",
            "payload": "message content or JSON"
        }
        """;

    /// <inheritdoc/>
    public string? JsonSchema => """{"type":"object","properties":{"neuron_id":{"type":"string"},"topic":{"type":"string"},"payload":{"type":"string"}},"required":["neuron_id","topic","payload"]}""";

    /// <inheritdoc/>
    public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        if (_ctx.Coordinator == null)
            return Task.FromResult(Result<string, string>.Failure("Autonomous coordinator not initialized."));

        try
        {
            var args = JsonSerializer.Deserialize<JsonElement>(input);
            var neuronId = args.GetProperty("neuron_id").GetString() ?? "";
            var topic = args.GetProperty("topic").GetString() ?? "";
            var payload = args.GetProperty("payload").GetString() ?? "";

            _ctx.Coordinator.SendToNeuron(neuronId, topic, payload);

            return Task.FromResult(Result<string, string>.Success(
                $"\ud83d\udce4 Message sent to `{neuronId}` on topic `{topic}`"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<string, string>.Failure($"Failed to send: {ex.Message}"));
        }
    }
}
