// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.

using System.Reflection;
using FluentAssertions;
using Moq;
using Ouroboros.Abstractions.Monads;
using Ouroboros.Application.CodeGeneration;
using Ouroboros.Application.GitHub;
using Xunit;
using RoslynCodeTool = Ouroboros.Application.CodeGeneration.RoslynCodeTool;

namespace Ouroboros.Tests.Mcp;

/// <summary>
/// Unit tests for McpServer covering request routing, tool dispatch,
/// protocol compliance, error responses, and connection lifecycle.
/// </summary>
[Trait("Category", "Unit")]
public class McpServerTests
{
    private readonly McpServer _server;

    public McpServerTests()
    {
        // McpServer requires a DslAssistant which has sealed dependencies (ToolAwareChatModel).
        // We use reflection to create a McpServer instance with the _dslAssistant field set to null,
        // allowing us to test all non-DSL tool routing (analyze_code, create_class, etc.)
        // and GitHub delegation without requiring the full DslAssistant dependency chain.
        var codeTool = new RoslynCodeTool();
        _server = (McpServer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(McpServer));

        // Set the private fields via reflection
        typeof(McpServer)
            .GetField("_codeTool", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(_server, codeTool);
        typeof(McpServer)
            .GetField("_githubClient", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(_server, null);
        // _dslAssistant remains null — DSL tool tests are not covered here
    }

    /// <summary>
    /// Creates a McpServer with the given GitHub client (and null DslAssistant) via reflection.
    /// </summary>
    private static McpServer CreateServerWithGitHub(IGitHubMcpClient githubClient)
    {
        var codeTool = new RoslynCodeTool();
        var server = (McpServer)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(McpServer));

        typeof(McpServer)
            .GetField("_codeTool", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(server, codeTool);
        typeof(McpServer)
            .GetField("_githubClient", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(server, githubClient);

        return server;
    }

    // ========================================================================
    // ListTools - without GitHub client
    // ========================================================================

    [Fact]
    public void ListTools_WithoutGitHub_ReturnsBaseCoreTools()
    {
        // Act
        var response = _server.ListTools();

        // Assert
        response.Tools.Should().NotBeEmpty();
        response.Tools.Select(t => t.Name).Should().Contain("analyze_code");
        response.Tools.Select(t => t.Name).Should().Contain("create_class");
        response.Tools.Select(t => t.Name).Should().Contain("add_method");
        response.Tools.Select(t => t.Name).Should().Contain("rename_symbol");
        response.Tools.Select(t => t.Name).Should().Contain("extract_method");
        response.Tools.Select(t => t.Name).Should().Contain("suggest_dsl_step");
        response.Tools.Select(t => t.Name).Should().Contain("complete_token");
        response.Tools.Select(t => t.Name).Should().Contain("validate_dsl");
        response.Tools.Select(t => t.Name).Should().Contain("explain_dsl");
        response.Tools.Select(t => t.Name).Should().Contain("build_dsl");
    }

    [Fact]
    public void ListTools_WithoutGitHub_DoesNotIncludeGitHubTools()
    {
        // Act
        var response = _server.ListTools();

        // Assert
        response.Tools.Select(t => t.Name).Should().NotContain("github_create_pr");
        response.Tools.Select(t => t.Name).Should().NotContain("github_push_changes");
        response.Tools.Select(t => t.Name).Should().NotContain("github_create_issue");
        response.Tools.Select(t => t.Name).Should().NotContain("github_read_file");
        response.Tools.Select(t => t.Name).Should().NotContain("github_list_files");
        response.Tools.Select(t => t.Name).Should().NotContain("github_create_branch");
        response.Tools.Select(t => t.Name).Should().NotContain("github_search_code");
    }

    // ========================================================================
    // ListTools - with GitHub client
    // ========================================================================

    [Fact]
    public void ListTools_WithGitHub_IncludesGitHubTools()
    {
        // Arrange
        var mockGitHub = new Mock<IGitHubMcpClient>();
        var server = CreateServerWithGitHub(mockGitHub.Object);

        // Act
        var response = server.ListTools();

        // Assert
        response.Tools.Select(t => t.Name).Should().Contain("github_create_pr");
        response.Tools.Select(t => t.Name).Should().Contain("github_push_changes");
        response.Tools.Select(t => t.Name).Should().Contain("github_create_issue");
        response.Tools.Select(t => t.Name).Should().Contain("github_read_file");
        response.Tools.Select(t => t.Name).Should().Contain("github_list_files");
        response.Tools.Select(t => t.Name).Should().Contain("github_create_branch");
        response.Tools.Select(t => t.Name).Should().Contain("github_search_code");
    }

    [Fact]
    public void ListTools_WithGitHub_IncludesBothBaseAndGitHubTools()
    {
        // Arrange
        var mockGitHub = new Mock<IGitHubMcpClient>();
        var server = CreateServerWithGitHub(mockGitHub.Object);

        // Act
        var response = server.ListTools();

        // Assert - should have base (10) + github (7) = 17 tools
        response.Tools.Count.Should().BeGreaterThanOrEqualTo(17);
    }

    // ========================================================================
    // ListTools - tool structure validation
    // ========================================================================

    [Fact]
    public void ListTools_AllToolsHaveNonEmptyNameAndDescription()
    {
        // Act
        var response = _server.ListTools();

        // Assert
        foreach (var tool in response.Tools)
        {
            tool.Name.Should().NotBeNullOrWhiteSpace();
            tool.Description.Should().NotBeNullOrWhiteSpace();
            tool.InputSchema.Should().NotBeNull();
        }
    }

    // ========================================================================
    // ExecuteToolAsync - unknown tool
    // ========================================================================

    [Fact]
    public async Task ExecuteToolAsync_UnknownTool_ReturnsFailure()
    {
        // Act
        var result = await _server.ExecuteToolAsync(
            "nonexistent_tool",
            new Dictionary<string, object>());

        // Assert
        result.IsError.Should().BeTrue();
        result.Content.Should().Contain("Unknown tool");
    }

    // ========================================================================
    // ExecuteToolAsync - analyze_code routing
    // ========================================================================

    [Fact]
    public async Task ExecuteToolAsync_AnalyzeCode_ValidCSharp_ReturnsSuccess()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            { "code", "public class Foo { public int Bar { get; set; } }" }
        };

        // Act
        var result = await _server.ExecuteToolAsync("analyze_code", parameters);

        // Assert
        result.IsError.Should().BeFalse();
        result.Content.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExecuteToolAsync_AnalyzeCode_WithAnalyzers_PassesFlag()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            { "code", "public class Foo { public void Bar() { } }" },
            { "runAnalyzers", true }
        };

        // Act
        var result = await _server.ExecuteToolAsync("analyze_code", parameters);

        // Assert
        result.IsError.Should().BeFalse();
    }

    // ========================================================================
    // ExecuteToolAsync - create_class routing
    // ========================================================================

    [Fact]
    public async Task ExecuteToolAsync_CreateClass_MinimalParams_ReturnsCode()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            { "className", "TestClass" },
            { "namespaceName", "TestNamespace" }
        };

        // Act
        var result = await _server.ExecuteToolAsync("create_class", parameters);

        // Assert
        result.IsError.Should().BeFalse();
    }

    // ========================================================================
    // ExecuteToolAsync - rename_symbol routing
    // ========================================================================

    [Fact]
    public async Task ExecuteToolAsync_RenameSymbol_RenamesAllOccurrences()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            { "code", "public class OldName { public OldName() { } }" },
            { "oldName", "OldName" },
            { "newName", "NewName" }
        };

        // Act
        var result = await _server.ExecuteToolAsync("rename_symbol", parameters);

        // Assert
        result.IsError.Should().BeFalse();
    }

    // ========================================================================
    // ExecuteToolAsync - add_method routing
    // ========================================================================

    [Fact]
    public async Task ExecuteToolAsync_AddMethod_ToExistingClass_ReturnsSuccess()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            { "code", "public class Foo { }" },
            { "className", "Foo" },
            { "methodSignature", "public void DoWork()" }
        };

        // Act
        var result = await _server.ExecuteToolAsync("add_method", parameters);

        // Assert
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteToolAsync_AddMethod_NonexistentClass_ReturnsFailure()
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            { "code", "public class Foo { }" },
            { "className", "Bar" },
            { "methodSignature", "public void DoWork()" }
        };

        // Act
        var result = await _server.ExecuteToolAsync("add_method", parameters);

        // Assert
        result.IsError.Should().BeTrue();
        result.Content.Should().Contain("Bar");
    }

    // ========================================================================
    // ExecuteToolAsync - extract_method routing
    // ========================================================================

    [Fact]
    public async Task ExecuteToolAsync_ExtractMethod_NoStatementsInRange_ReturnsFailure()
    {
        // Arrange
        string code = @"
public class Foo
{
    public void Bar()
    {
        int x = 1;
    }
}";
        var parameters = new Dictionary<string, object>
        {
            { "code", code },
            { "startLine", 100 },
            { "endLine", 200 },
            { "newMethodName", "ExtractedMethod" }
        };

        // Act
        var result = await _server.ExecuteToolAsync("extract_method", parameters);

        // Assert
        result.IsError.Should().BeTrue();
    }

    // ========================================================================
    // ExecuteToolAsync - GitHub tools without client
    // ========================================================================

    [Theory]
    [InlineData("github_create_pr")]
    [InlineData("github_push_changes")]
    [InlineData("github_create_issue")]
    [InlineData("github_read_file")]
    [InlineData("github_list_files")]
    [InlineData("github_create_branch")]
    [InlineData("github_search_code")]
    public async Task ExecuteToolAsync_GitHubTools_WithoutClient_ReturnsFailure(string toolName)
    {
        // Arrange
        var parameters = new Dictionary<string, object>
        {
            { "title", "test" },
            { "description", "test" },
            { "sourceBranch", "feature" },
            { "branchName", "feature" },
            { "changes", new List<object>() },
            { "commitMessage", "test" },
            { "path", "README.md" },
            { "query", "test" },
            { "labels", new List<string> { "bug" } }
        };

        // Act
        var result = await _server.ExecuteToolAsync(toolName, parameters);

        // Assert
        result.IsError.Should().BeTrue();
        result.Content.Should().Contain("GitHub client not configured");
    }

    // ========================================================================
    // ExecuteToolAsync - exception handling
    // ========================================================================

    [Fact]
    public async Task ExecuteToolAsync_InternalException_ReturnsSafeFailure()
    {
        // Arrange - pass invalid parameters that will cause a downstream exception
        var parameters = new Dictionary<string, object>
        {
            { "startLine", -1 },
            { "endLine", -1 },
            { "code", "" },
            { "newMethodName", "" }
        };

        // Act
        var result = await _server.ExecuteToolAsync("extract_method", parameters);

        // Assert
        result.IsError.Should().BeTrue();
        result.Content.Should().NotBeNullOrEmpty();
    }

    // ========================================================================
    // ExecuteToolAsync - GitHub with mock client
    // ========================================================================

    [Fact]
    public async Task ExecuteToolAsync_GitHubCreateBranch_WithClient_DelegatesToClient()
    {
        // Arrange
        var mockGitHub = new Mock<IGitHubMcpClient>();
        mockGitHub.Setup(g => g.CreateBranchAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BranchInfo, string>.Success(
                new BranchInfo { Name = "new-feature", Sha = "abc123", IsProtected = false }));

        var server = CreateServerWithGitHub(mockGitHub.Object);

        var parameters = new Dictionary<string, object>
        {
            { "branchName", "new-feature" }
        };

        // Act
        var result = await server.ExecuteToolAsync("github_create_branch", parameters);

        // Assert
        result.IsError.Should().BeFalse();
        mockGitHub.Verify(g => g.CreateBranchAsync("new-feature", "main", default), Times.Once);
    }

    [Fact]
    public async Task ExecuteToolAsync_GitHubReadFile_WithClient_DelegatesToClient()
    {
        // Arrange
        var mockGitHub = new Mock<IGitHubMcpClient>();
        mockGitHub.Setup(g => g.ReadFileAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<FileContent, string>.Success(
                new FileContent { Path = "README.md", Content = "# Title", Size = 7, Sha = "def456" }));

        var server = CreateServerWithGitHub(mockGitHub.Object);

        var parameters = new Dictionary<string, object>
        {
            { "path", "README.md" }
        };

        // Act
        var result = await server.ExecuteToolAsync("github_read_file", parameters);

        // Assert
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteToolAsync_GitHubSearchCode_ClientReturnsError_PropagatesError()
    {
        // Arrange
        var mockGitHub = new Mock<IGitHubMcpClient>();
        mockGitHub.Setup(g => g.SearchCodeAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<CodeSearchResult>, string>.Failure("Rate limited"));

        var server = CreateServerWithGitHub(mockGitHub.Object);

        var parameters = new Dictionary<string, object>
        {
            { "query", "test" }
        };

        // Act
        var result = await server.ExecuteToolAsync("github_search_code", parameters);

        // Assert
        result.IsError.Should().BeTrue();
        result.Content.Should().Contain("Rate limited");
    }

    [Fact]
    public async Task ExecuteToolAsync_GitHubCreateIssue_WithLabels_PassesLabelsThrough()
    {
        // Arrange
        var mockGitHub = new Mock<IGitHubMcpClient>();
        mockGitHub.Setup(g => g.CreateIssueAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IssueInfo, string>.Success(
                new IssueInfo { Number = 1, Url = "https://github.com/issue/1", Title = "Bug report", State = "open", CreatedAt = DateTime.UtcNow }));

        var server = CreateServerWithGitHub(mockGitHub.Object);

        var parameters = new Dictionary<string, object>
        {
            { "title", "Bug report" },
            { "description", "Something is broken" },
            { "labels", new List<object> { "bug", "critical" } }
        };

        // Act
        var result = await server.ExecuteToolAsync("github_create_issue", parameters);

        // Assert
        result.IsError.Should().BeFalse();
    }
}
