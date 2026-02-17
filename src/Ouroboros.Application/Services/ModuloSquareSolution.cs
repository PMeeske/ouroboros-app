using System.Numerics;

namespace Ouroboros.Application.Services;

/// <summary>
/// Represents a solution to a modulo-square theory problem.
/// </summary>
public record ModuloSquareSolution
{
    /// <summary>Gets the target value.</summary>
    public BigInteger Target { get; init; }

    /// <summary>Gets the square root found.</summary>
    public BigInteger SquareRoot { get; init; }

    /// <summary>Gets the derivation chain.</summary>
    public required string Derivation { get; init; }

    /// <summary>Gets the timestamp.</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>Gets whether the solution is verified.</summary>
    public bool IsVerified { get; init; }
}