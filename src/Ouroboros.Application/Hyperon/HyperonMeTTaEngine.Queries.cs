// <copyright file="HyperonMeTTaEngine.Queries.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Abstractions;

namespace Ouroboros.Application.Hyperon;

using Ouroboros.Core.Hyperon;
using Unit = Unit;

/// <summary>
/// Partial class containing query, evaluation, and source loading operations
/// for the HyperonMeTTaEngine.
/// </summary>
public sealed partial class HyperonMeTTaEngine
{
    /// <summary>
    /// Evaluates a query and returns results with their bindings.
    /// </summary>
    /// <param name="query">The query string.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of (result atom, bindings) pairs.</returns>
    public Task<Result<IReadOnlyList<(Atom Result, Substitution Bindings)>, string>> EvaluateWithBindingsAsync(
        string query, CancellationToken ct = default)
    {
        if (_disposed)
            return Task.FromResult(Result<IReadOnlyList<(Atom, Substitution)>, string>.Failure("Engine disposed"));

        try
        {
            var parseResult = _parser.Parse(query);
            if (!parseResult.IsSuccess)
            {
                return Task.FromResult(Result<IReadOnlyList<(Atom, Substitution)>, string>.Failure($"Parse error: {parseResult.Error}"));
            }

            var results = _interpreter.EvaluateWithBindings(parseResult.Value).ToList();
            return Task.FromResult(Result<IReadOnlyList<(Atom, Substitution)>, string>.Success(results));
        }
        catch (ArgumentException ex)
        {
            return Task.FromResult(Result<IReadOnlyList<(Atom, Substitution)>, string>.Failure($"Evaluation failed: {ex.Message}"));
        }
        catch (InvalidOperationException ex)
        {
            return Task.FromResult(Result<IReadOnlyList<(Atom, Substitution)>, string>.Failure($"Evaluation failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Queries the space for pattern matches.
    /// </summary>
    /// <param name="pattern">The pattern to match.</param>
    /// <returns>List of matching atoms with their bindings.</returns>
    public IReadOnlyList<(Atom Atom, Substitution Bindings)> Query(Atom pattern)
    {
        return _space.Query(pattern).ToList();
    }

    /// <summary>
    /// Gets all atoms in the space.
    /// </summary>
    /// <returns>All atoms.</returns>
    public IEnumerable<Atom> GetAllAtoms() => _space.All();

    /// <summary>
    /// Gets the count of atoms in the space.
    /// </summary>
    public int AtomCount => _space.Count;

    /// <summary>
    /// Adds multiple atoms from MeTTa source code.
    /// </summary>
    /// <param name="mettaSource">Multi-line MeTTa source code.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of atoms successfully added.</returns>
    public async Task<int> LoadMeTTaSourceAsync(string mettaSource, CancellationToken ct = default)
    {
        int added = 0;
        var lines = mettaSource.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";"))
                continue;

            var result = await AddFactAsync(trimmed, ct);
            if (result.IsSuccess)
                added++;
        }

        return added;
    }

    /// <summary>
    /// Exports all atoms as MeTTa source code.
    /// </summary>
    /// <returns>MeTTa source code string.</returns>
    public string ExportToMeTTa()
    {
        return string.Join("\n", _space.All().Select(a => a.ToSExpr()));
    }
}
