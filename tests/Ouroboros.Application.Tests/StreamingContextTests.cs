using FluentAssertions;
using Ouroboros.Application;
using Xunit;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public class StreamingContextTests
{
    [Fact]
    public void Constructor_ShouldNotBeDisposed()
    {
        using var ctx = new StreamingContext();

        ctx.IsDisposed.Should().BeFalse();
    }

    [Fact]
    public void Dispose_ShouldSetIsDisposed()
    {
        var ctx = new StreamingContext();

        ctx.Dispose();

        ctx.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void Dispose_CalledTwice_ShouldBeIdempotent()
    {
        var ctx = new StreamingContext();

        ctx.Dispose();
        ctx.Dispose();

        ctx.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void Register_NullDisposable_ShouldThrow()
    {
        using var ctx = new StreamingContext();

        var act = () => ctx.Register(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Register_AfterDispose_ShouldDisposeImmediately()
    {
        var ctx = new StreamingContext();
        ctx.Dispose();
        bool disposed = false;
        var disposable = System.Reactive.Disposables.Disposable.Create(() => disposed = true);

        ctx.Register(disposable);

        disposed.Should().BeTrue();
    }

    [Fact]
    public void RegisterCleanup_NullAction_ShouldThrow()
    {
        using var ctx = new StreamingContext();

        var act = () => ctx.RegisterCleanup(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RegisterCleanup_ShouldExecuteOnDispose()
    {
        bool cleaned = false;
        var ctx = new StreamingContext();
        ctx.RegisterCleanup(() => cleaned = true);

        ctx.Dispose();

        cleaned.Should().BeTrue();
    }

    [Fact]
    public void Register_ValidDisposable_ShouldDisposeOnContextDispose()
    {
        bool disposed = false;
        var ctx = new StreamingContext();
        var disposable = System.Reactive.Disposables.Disposable.Create(() => disposed = true);
        ctx.Register(disposable);

        ctx.Dispose();

        disposed.Should().BeTrue();
    }
}
