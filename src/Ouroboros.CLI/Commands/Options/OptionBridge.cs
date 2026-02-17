// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI;

/// <summary>
/// Bridge type that wraps <see cref="Ouroboros.Abstractions.Monads.Option{T}"/> to avoid
/// name collision with <see cref="System.CommandLine.Option{T}"/>.
/// <para>
/// Ouroboros.Abstractions.Monads is NOT globally imported because its Option&lt;T&gt; conflicts
/// with System.CommandLine.Option&lt;T&gt; used across the CLI Options infrastructure.
/// This bridge provides implicit conversions so code can use <c>Maybe&lt;T&gt;</c> seamlessly.
/// </para>
/// </summary>
/// <typeparam name="T">The type of the optional value.</typeparam>
public readonly struct Maybe<T>(Ouroboros.Abstractions.Monads.Option<T> inner)
{
    private readonly Ouroboros.Abstractions.Monads.Option<T> _inner = inner;

    public T? Value => _inner.Value;
    public bool HasValue => _inner.HasValue;

    public T GetValueOrDefault(T defaultValue) => _inner.GetValueOrDefault(defaultValue);

    public static implicit operator Maybe<T>(Ouroboros.Abstractions.Monads.Option<T> option) => new(option);
    public static implicit operator Ouroboros.Abstractions.Monads.Option<T>(Maybe<T> maybe) => maybe._inner;

    public override string ToString() => HasValue ? $"Some({Value})" : "None";
}
