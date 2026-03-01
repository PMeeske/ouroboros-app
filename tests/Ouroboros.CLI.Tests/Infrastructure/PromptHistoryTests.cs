using Ouroboros.CLI.Infrastructure;

namespace Ouroboros.Tests.CLI.Infrastructure;

[Trait("Category", "Unit")]
public class PromptHistoryTests
{
    private readonly PromptHistory _history = new();

    [Fact]
    public void Count_Initially_IsZero()
    {
        _history.Count.Should().Be(0);
    }

    [Fact]
    public void Push_AddsEntry()
    {
        _history.Push("hello");

        _history.Count.Should().Be(1);
    }

    [Fact]
    public void Push_MultipleEntries_IncrementsCount()
    {
        _history.Push("first");
        _history.Push("second");
        _history.Push("third");

        _history.Count.Should().Be(3);
    }

    [Fact]
    public void Push_EmptyString_DoesNotAdd()
    {
        _history.Push("");

        _history.Count.Should().Be(0);
    }

    [Fact]
    public void Push_WhitespaceOnly_DoesNotAdd()
    {
        _history.Push("   ");

        _history.Count.Should().Be(0);
    }

    [Fact]
    public void Push_ConsecutiveDuplicates_Deduplicates()
    {
        _history.Push("hello");
        _history.Push("hello");

        _history.Count.Should().Be(1);
    }

    [Fact]
    public void Push_NonConsecutiveDuplicates_KeepsBoth()
    {
        _history.Push("hello");
        _history.Push("world");
        _history.Push("hello");

        _history.Count.Should().Be(3);
    }

    [Fact]
    public void NavigateUp_EmptyHistory_ReturnsNull()
    {
        var result = _history.NavigateUp("draft");

        result.Should().BeNull();
    }

    [Fact]
    public void NavigateUp_WithHistory_ReturnsMostRecent()
    {
        _history.Push("first");
        _history.Push("second");

        var result = _history.NavigateUp("");

        result.Should().Be("second");
    }

    [Fact]
    public void NavigateUp_MultipleTimes_WalksBackward()
    {
        _history.Push("first");
        _history.Push("second");
        _history.Push("third");

        var r1 = _history.NavigateUp("");
        var r2 = _history.NavigateUp("");
        var r3 = _history.NavigateUp("");

        r1.Should().Be("third");
        r2.Should().Be("second");
        r3.Should().Be("first");
    }

    [Fact]
    public void NavigateUp_BeyondHistory_StaysAtOldest()
    {
        _history.Push("only");

        _history.NavigateUp("");
        var result = _history.NavigateUp("");

        result.Should().Be("only");
    }

    [Fact]
    public void NavigateDown_WithoutNavigateUp_ReturnsDraft()
    {
        var result = _history.NavigateDown();

        result.Should().BeEmpty();
    }

    [Fact]
    public void NavigateDown_AfterUp_ReturnsNewerEntry()
    {
        _history.Push("first");
        _history.Push("second");
        _history.Push("third");

        _history.NavigateUp("draft");
        _history.NavigateUp("draft");
        var result = _history.NavigateDown();

        result.Should().Be("third");
    }

    [Fact]
    public void NavigateDown_PastNewest_ReturnsDraft()
    {
        _history.Push("entry");

        _history.NavigateUp("my draft");
        var result = _history.NavigateDown();

        result.Should().Be("my draft");
    }

    [Fact]
    public void IsNavigating_Initially_IsFalse()
    {
        _history.IsNavigating.Should().BeFalse();
    }

    [Fact]
    public void IsNavigating_AfterUp_IsTrue()
    {
        _history.Push("entry");
        _history.NavigateUp("");

        _history.IsNavigating.Should().BeTrue();
    }

    [Fact]
    public void IsNavigating_AfterDownToBottom_IsFalse()
    {
        _history.Push("entry");
        _history.NavigateUp("");
        _history.NavigateDown();

        _history.IsNavigating.Should().BeFalse();
    }

    [Fact]
    public void CancelNavigation_RestoresDraft()
    {
        _history.Push("entry");
        _history.NavigateUp("my draft text");

        var result = _history.CancelNavigation();

        result.Should().Be("my draft text");
        _history.IsNavigating.Should().BeFalse();
    }

    [Fact]
    public void Push_ResetsNavigation()
    {
        _history.Push("first");
        _history.NavigateUp("");
        _history.Push("second");

        _history.IsNavigating.Should().BeFalse();
    }
}
