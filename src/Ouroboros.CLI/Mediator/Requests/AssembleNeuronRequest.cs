using MediatR;
using Ouroboros.Application.SelfAssembly;
using Ouroboros.Domain.Autonomous;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR request that attempts to assemble a neuron from a blueprint
/// through the full self-assembly pipeline.
/// </summary>
public sealed record AssembleNeuronRequest(NeuronBlueprint Blueprint) : IRequest<Neuron?>;
