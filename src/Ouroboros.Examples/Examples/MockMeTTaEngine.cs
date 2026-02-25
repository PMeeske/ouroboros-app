using Ouroboros.Abstractions;

namespace Ouroboros.Examples;

/// <summary>
/// Mock MeTTa engine for testing when MeTTa is not installed.
/// </summary>
internal sealed class MockMeTTaEngine : IMeTTaEngine
{
    private readonly List<string> facts = new();

    public Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
    {
        string result = query.Contains("match")
            ? "[Mock query result]"
            : "3";
        return Task.FromResult(Result<string, string>.Success(result));
    }

    public Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
    {
        this.facts.Add(fact);
        return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
    }

    public Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
    {
        return Task.FromResult(Result<string, string>.Success($"Rule applied: {rule}"));
    }

    public Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
    {
        return Task.FromResult(Result<bool, string>.Success(true));
    }

    public Task<Result<Unit, string>> ResetAsync(CancellationToken ct = default)
    {
        this.facts.Clear();
        return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
    }

    public void Dispose()
    {
        // Nothing to dispose
    }
}