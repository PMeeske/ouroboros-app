namespace Ouroboros.Application.Personality;

/// <summary>
/// Base class for thought providers with common functionality.
/// </summary>
public abstract class ThoughtProviderBase : IThoughtProvider
{
    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <inheritdoc/>
    public virtual int Order => 100;

    /// <inheritdoc/>
    public virtual bool CanProcess(ThoughtContext context) => true;

    /// <inheritdoc/>
    public abstract Task<ThoughtProviderResult> GenerateThoughtsAsync(ThoughtContext context, CancellationToken ct = default);

    /// <summary>Selects a random template from the array.</summary>
    protected static string SelectTemplate(string[] templates, Random random) =>
        templates[random.Next(templates.Length)];
}