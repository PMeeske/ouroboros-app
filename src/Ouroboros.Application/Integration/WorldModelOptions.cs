namespace Ouroboros.Application.Integration;

/// <summary>Options for world model configuration.</summary>
public sealed record WorldModelOptions(
    int StateSpaceSize = 128,
    int ActionSpaceSize = 64,
    double DiscountFactor = 0.99);