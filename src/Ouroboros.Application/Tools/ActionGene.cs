namespace Ouroboros.Application.Tools;

/// <summary>
/// Gene for evolving action sequences in the genetic algorithm.
/// </summary>
public sealed record ActionGene(string ActionType, string ActionName, double Priority);