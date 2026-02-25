using Ouroboros.Core.Hyperon;
using Ouroboros.Core.Hyperon.Parsing;

namespace Ouroboros.Application.Hyperon;

/// <summary>
/// Represents a Hyperon-based reasoning flow.
/// </summary>
public sealed class HyperonFlow : IAsyncDisposable
{
    private readonly string _name;
    private readonly HyperonMeTTaEngine _engine;
    private readonly string? _description;
    private readonly List<FlowStep> _steps = new();
    private readonly SExpressionParser _parser = new();

    /// <summary>
    /// Gets the flow name.
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// Gets the flow description.
    /// </summary>
    public string? Description => _description;

    /// <summary>
    /// Gets the flow steps.
    /// </summary>
    public IReadOnlyList<FlowStep> Steps => _steps.AsReadOnly();

    /// <summary>
    /// Initializes a new instance of the <see cref="HyperonFlow"/> class.
    /// </summary>
    public HyperonFlow(string name, HyperonMeTTaEngine engine, string? description = null)
    {
        _name = name;
        _engine = engine;
        _description = description;
    }

    /// <summary>
    /// Adds a fact-loading step.
    /// </summary>
    /// <param name="facts">MeTTa facts to load.</param>
    /// <returns>This flow for chaining.</returns>
    public HyperonFlow LoadFacts(params string[] facts)
    {
        _steps.Add(new FlowStep
        {
            StepType = FlowStepType.LoadFacts,
            Data = facts
        });
        return this;
    }

    /// <summary>
    /// Adds a rule application step.
    /// </summary>
    /// <param name="rule">MeTTa rule to apply.</param>
    /// <returns>This flow for chaining.</returns>
    public HyperonFlow ApplyRule(string rule)
    {
        _steps.Add(new FlowStep
        {
            StepType = FlowStepType.ApplyRule,
            Data = rule
        });
        return this;
    }

    /// <summary>
    /// Adds a query step.
    /// </summary>
    /// <param name="query">MeTTa query to execute.</param>
    /// <returns>This flow for chaining.</returns>
    public HyperonFlow Query(string query)
    {
        _steps.Add(new FlowStep
        {
            StepType = FlowStepType.Query,
            Data = query
        });
        return this;
    }

    /// <summary>
    /// Adds a transformation step.
    /// </summary>
    /// <param name="transformer">Function to transform results.</param>
    /// <returns>This flow for chaining.</returns>
    public HyperonFlow Transform(Func<IReadOnlyList<Atom>, IEnumerable<Atom>> transformer)
    {
        _steps.Add(new FlowStep
        {
            StepType = FlowStepType.Transform,
            Data = transformer
        });
        return this;
    }

    /// <summary>
    /// Adds a filter step.
    /// </summary>
    /// <param name="predicate">Predicate to filter atoms.</param>
    /// <returns>This flow for chaining.</returns>
    public HyperonFlow Filter(Func<Atom, bool> predicate)
    {
        _steps.Add(new FlowStep
        {
            StepType = FlowStepType.Filter,
            Data = predicate
        });
        return this;
    }

    /// <summary>
    /// Adds a side-effect step (e.g., logging).
    /// </summary>
    /// <param name="action">Action to perform on results.</param>
    /// <returns>This flow for chaining.</returns>
    public HyperonFlow SideEffect(Action<IReadOnlyList<Atom>> action)
    {
        _steps.Add(new FlowStep
        {
            StepType = FlowStepType.SideEffect,
            Data = action
        });
        return this;
    }

    /// <summary>
    /// Executes the flow with the given input.
    /// </summary>
    /// <param name="input">Input MeTTa source or query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result atoms from the flow.</returns>
    public async Task<Result<IReadOnlyList<Atom>, string>> ExecuteAsync(string input, CancellationToken ct = default)
    {
        var currentResults = new List<Atom>();

        // Parse and add input
        if (!string.IsNullOrWhiteSpace(input))
        {
            var parseResult = _parser.Parse(input);
            if (parseResult.IsSuccess)
            {
                _engine.AddAtom(parseResult.Value);
                currentResults.Add(parseResult.Value);
            }
        }

        // Execute each step
        foreach (var step in _steps)
        {
            try
            {
                switch (step.StepType)
                {
                    case FlowStepType.LoadFacts:
                        if (step.Data is string[] facts)
                        {
                            foreach (var fact in facts)
                            {
                                await _engine.AddFactAsync(fact, ct);
                            }
                        }
                        break;

                    case FlowStepType.ApplyRule:
                        if (step.Data is string rule)
                        {
                            await _engine.ApplyRuleAsync(rule, ct);
                        }
                        break;

                    case FlowStepType.Query:
                        if (step.Data is string query)
                        {
                            var parseResult = _parser.Parse(query);
                            if (parseResult.IsSuccess)
                            {
                                var results = _engine.Interpreter.Evaluate(parseResult.Value).ToList();
                                currentResults = results;
                            }
                        }
                        break;

                    case FlowStepType.Transform:
                        if (step.Data is Func<IReadOnlyList<Atom>, IEnumerable<Atom>> transformer)
                        {
                            currentResults = transformer(currentResults).ToList();
                        }
                        break;

                    case FlowStepType.Filter:
                        if (step.Data is Func<Atom, bool> predicate)
                        {
                            currentResults = currentResults.Where(predicate).ToList();
                        }
                        break;

                    case FlowStepType.SideEffect:
                        if (step.Data is Action<IReadOnlyList<Atom>> action)
                        {
                            action(currentResults);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                return Result<IReadOnlyList<Atom>, string>.Failure($"Step {step.StepType} failed: {ex.Message}");
            }
        }

        return Result<IReadOnlyList<Atom>, string>.Success(currentResults);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _steps.Clear();
        return ValueTask.CompletedTask;
    }
}