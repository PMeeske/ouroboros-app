using FluentAssertions;
using Ouroboros.Application.GitHub;
using Xunit;

namespace Ouroboros.Tests.GitHub;

[Trait("Category", "Unit")]
public class GitHubModelsTests
{
    // --- BranchInfo ---

    [Fact]
    public void BranchInfo_ShouldSetRequiredProperties()
    {
        var info = new BranchInfo
        {
            Name = "main",
            Sha = "abc123",
            IsProtected = true
        };

        info.Name.Should().Be("main");
        info.Sha.Should().Be("abc123");
        info.IsProtected.Should().BeTrue();
    }

    // --- CodeSearchResult ---

    [Fact]
    public void CodeSearchResult_ShouldSetProperties()
    {
        var result = new CodeSearchResult
        {
            Path = "src/test.cs",
            Filename = "test.cs",
            MatchedContent = "public class Test",
            LineNumber = 10
        };

        result.Path.Should().Be("src/test.cs");
        result.Filename.Should().Be("test.cs");
        result.MatchedContent.Should().Be("public class Test");
        result.LineNumber.Should().Be(10);
    }

    [Fact]
    public void CodeSearchResult_LineNumber_ShouldBeNullable()
    {
        var result = new CodeSearchResult
        {
            Path = "src/test.cs",
            Filename = "test.cs",
            MatchedContent = "content"
        };

        result.LineNumber.Should().BeNull();
    }

    // --- CommitInfo ---

    [Fact]
    public void CommitInfo_ShouldSetProperties()
    {
        var now = DateTime.UtcNow;
        var info = new CommitInfo
        {
            Sha = "def456",
            Message = "Fix bug",
            Url = "https://github.com/test/commit/def456",
            CommittedAt = now
        };

        info.Sha.Should().Be("def456");
        info.Message.Should().Be("Fix bug");
        info.Url.Should().Contain("def456");
        info.CommittedAt.Should().Be(now);
    }

    // --- FileChange ---

    [Fact]
    public void FileChange_ShouldSetProperties()
    {
        var change = new FileChange
        {
            Path = "src/file.cs",
            Content = "new content",
            ChangeType = FileChangeType.Update
        };

        change.Path.Should().Be("src/file.cs");
        change.Content.Should().Be("new content");
        change.ChangeType.Should().Be(FileChangeType.Update);
    }

    // --- FileChangeType ---

    [Fact]
    public void FileChangeType_ShouldHave3Values()
    {
        Enum.GetValues<FileChangeType>().Should().HaveCount(3);
    }

    [Fact]
    public void FileChangeType_ShouldContainExpectedValues()
    {
        var values = Enum.GetValues<FileChangeType>();

        values.Should().Contain(FileChangeType.Create);
        values.Should().Contain(FileChangeType.Update);
        values.Should().Contain(FileChangeType.Delete);
    }

    // --- FileContent ---

    [Fact]
    public void FileContent_ShouldSetProperties()
    {
        var content = new FileContent
        {
            Path = "src/file.cs",
            Content = "class A {}",
            Size = 10,
            Sha = "sha123"
        };

        content.Path.Should().Be("src/file.cs");
        content.Content.Should().Be("class A {}");
        content.Size.Should().Be(10);
        content.Sha.Should().Be("sha123");
    }

    // --- GitHubFileInfo ---

    [Fact]
    public void GitHubFileInfo_ShouldSetProperties()
    {
        var info = new GitHubFileInfo
        {
            Name = "file.cs",
            Path = "src/file.cs",
            Type = "file",
            Size = 100
        };

        info.Name.Should().Be("file.cs");
        info.Path.Should().Be("src/file.cs");
        info.Type.Should().Be("file");
        info.Size.Should().Be(100);
    }

    [Fact]
    public void GitHubFileInfo_Directory_ShouldHaveNullSize()
    {
        var info = new GitHubFileInfo
        {
            Name = "src",
            Path = "src",
            Type = "dir"
        };

        info.Size.Should().BeNull();
    }

    // --- IssueInfo ---

    [Fact]
    public void IssueInfo_ShouldSetProperties()
    {
        var now = DateTime.UtcNow;
        var info = new IssueInfo
        {
            Number = 42,
            Url = "https://github.com/test/issues/42",
            Title = "Bug report",
            State = "open",
            CreatedAt = now
        };

        info.Number.Should().Be(42);
        info.Title.Should().Be("Bug report");
        info.State.Should().Be("open");
        info.CreatedAt.Should().Be(now);
    }

    // --- PullRequestInfo ---

    [Fact]
    public void PullRequestInfo_ShouldSetProperties()
    {
        var now = DateTime.UtcNow;
        var info = new PullRequestInfo
        {
            Number = 99,
            Url = "https://github.com/test/pulls/99",
            Title = "Add feature",
            State = "open",
            HeadBranch = "feature/test",
            BaseBranch = "main",
            CreatedAt = now
        };

        info.Number.Should().Be(99);
        info.Title.Should().Be("Add feature");
        info.State.Should().Be("open");
        info.HeadBranch.Should().Be("feature/test");
        info.BaseBranch.Should().Be("main");
    }

    // --- PullRequestStatus ---

    [Fact]
    public void PullRequestStatus_ShouldSetProperties()
    {
        var status = new PullRequestStatus
        {
            Number = 99,
            State = "open",
            Mergeable = true,
            Additions = 50,
            Deletions = 10,
            ChangedFiles = 3
        };

        status.Number.Should().Be(99);
        status.State.Should().Be("open");
        status.Mergeable.Should().BeTrue();
        status.Additions.Should().Be(50);
        status.Deletions.Should().Be(10);
        status.ChangedFiles.Should().Be(3);
    }

    [Fact]
    public void PullRequestStatus_Mergeable_ShouldBeNullable()
    {
        var status = new PullRequestStatus
        {
            Number = 1,
            State = "open",
            Additions = 0,
            Deletions = 0,
            ChangedFiles = 0
        };

        status.Mergeable.Should().BeNull();
    }

    // --- GitHubMcpClientOptions ---

    [Fact]
    public void GitHubMcpClientOptions_ShouldHaveDefaults()
    {
        var options = new GitHubMcpClientOptions
        {
            Owner = "testowner",
            Repository = "testrepo",
            Token = "ghp_test123"
        };

        options.BaseUrl.Should().Be("https://api.github.com");
        options.Timeout.Should().Be(TimeSpan.FromSeconds(30));
        options.MaxRetries.Should().Be(3);
        options.RequireEthicsApproval.Should().BeTrue();
    }

    [Fact]
    public void GitHubMcpClientOptions_IsValid_ValidOptions_ShouldReturnTrue()
    {
        var options = new GitHubMcpClientOptions
        {
            Owner = "testowner",
            Repository = "testrepo",
            Token = "ghp_test123"
        };

        options.IsValid().Should().BeTrue();
    }

    [Fact]
    public void GitHubMcpClientOptions_IsValid_EmptyOwner_ShouldReturnFalse()
    {
        var options = new GitHubMcpClientOptions
        {
            Owner = "",
            Repository = "testrepo",
            Token = "ghp_test123"
        };

        options.IsValid().Should().BeFalse();
    }

    [Fact]
    public void GitHubMcpClientOptions_IsValid_EmptyToken_ShouldReturnFalse()
    {
        var options = new GitHubMcpClientOptions
        {
            Owner = "testowner",
            Repository = "testrepo",
            Token = ""
        };

        options.IsValid().Should().BeFalse();
    }

    [Fact]
    public void GitHubMcpClientOptions_IsValid_ZeroTimeout_ShouldReturnFalse()
    {
        var options = new GitHubMcpClientOptions
        {
            Owner = "testowner",
            Repository = "testrepo",
            Token = "ghp_test123",
            Timeout = TimeSpan.Zero
        };

        options.IsValid().Should().BeFalse();
    }

    [Fact]
    public void GitHubMcpClientOptions_IsValid_NegativeRetries_ShouldReturnFalse()
    {
        var options = new GitHubMcpClientOptions
        {
            Owner = "testowner",
            Repository = "testrepo",
            Token = "ghp_test123",
            MaxRetries = -1
        };

        options.IsValid().Should().BeFalse();
    }

    // --- SelfModificationResult ---

    [Fact]
    public void SelfModificationResult_Success_ShouldSetProperties()
    {
        var pr = new PullRequestInfo
        {
            Number = 1,
            Url = "https://github.com/test/pulls/1",
            Title = "Self-mod",
            State = "open",
            HeadBranch = "self-mod/change",
            BaseBranch = "main",
            CreatedAt = DateTime.UtcNow
        };

        var clearance = Ouroboros.Core.Ethics.EthicalClearance.Permitted("Self-mod approved");
        var result = new SelfModificationResult(
            true, pr, "self-mod/change", null,
            clearance);

        result.Success.Should().BeTrue();
        result.PullRequest.Should().NotBeNull();
        result.BranchName.Should().Be("self-mod/change");
        result.Error.Should().BeNull();
        result.EthicsClearance.IsPermitted.Should().BeTrue();
    }

    [Fact]
    public void SelfModificationResult_Failure_ShouldSetProperties()
    {
        var denied = Ouroboros.Core.Ethics.EthicalClearance.Denied(
            "Ethics denied",
            Array.Empty<Ouroboros.Core.Ethics.EthicalViolation>());
        var result = new SelfModificationResult(
            false, null, null, "Ethics denied",
            denied);

        result.Success.Should().BeFalse();
        result.PullRequest.Should().BeNull();
        result.Error.Should().Be("Ethics denied");
    }
}
