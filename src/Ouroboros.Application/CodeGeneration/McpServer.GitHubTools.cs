using System.Text.Json;
using Ouroboros.Application.GitHub;
using Ouroboros.Application.Mcp;

namespace Ouroboros.Application.CodeGeneration;

/// <summary>
/// GitHub tool execution methods for the MCP server.
/// </summary>
public partial class McpServer
{
    /// <summary>
    /// Returns the GitHub tool definitions for ListTools registration.
    /// </summary>
    private McpTool[] GetGitHubToolDefinitions() => new[]
    {
        new McpTool
        {
            Name = "github_create_pr",
            Description = "Create a pull request for self-modification. Requires ethics approval.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    title = new { type = "string", description = "PR title" },
                    description = new { type = "string", description = "PR description" },
                    sourceBranch = new { type = "string", description = "Source branch" },
                    targetBranch = new { type = "string", description = "Target branch (default: main)" }
                },
                required = new[] { "title", "description", "sourceBranch" }
            }
        },
        new McpTool
        {
            Name = "github_push_changes",
            Description = "Push code changes to a branch. Requires ethics approval.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    branchName = new { type = "string", description = "Branch name" },
                    changes = new { type = "array", description = "File changes to push" },
                    commitMessage = new { type = "string", description = "Commit message" }
                },
                required = new[] { "branchName", "changes", "commitMessage" }
            }
        },
        new McpTool
        {
            Name = "github_create_issue",
            Description = "Create an issue for improvement proposal.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    title = new { type = "string", description = "Issue title" },
                    description = new { type = "string", description = "Issue description" },
                    labels = new { type = "array", items = new { type = "string" }, description = "Issue labels" }
                },
                required = new[] { "title", "description" }
            }
        },
        new McpTool
        {
            Name = "github_read_file",
            Description = "Read a file from the repository.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    path = new { type = "string", description = "File path" },
                    branch = new { type = "string", description = "Branch name (optional)" }
                },
                required = new[] { "path" }
            }
        },
        new McpTool
        {
            Name = "github_list_files",
            Description = "List files in a directory.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    path = new { type = "string", description = "Directory path (default: root)" },
                    branch = new { type = "string", description = "Branch name (optional)" }
                },
                required = Array.Empty<string>()
            }
        },
        new McpTool
        {
            Name = "github_create_branch",
            Description = "Create a new branch for modifications. Requires ethics approval.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    branchName = new { type = "string", description = "New branch name" },
                    baseBranch = new { type = "string", description = "Base branch (default: main)" }
                },
                required = new[] { "branchName" }
            }
        },
        new McpTool
        {
            Name = "github_search_code",
            Description = "Search code in the repository.",
            InputSchema = new
            {
                type = "object",
                properties = new
                {
                    query = new { type = "string", description = "Search query" },
                    path = new { type = "string", description = "Path filter (optional)" }
                },
                required = new[] { "query" }
            }
        }
    };

    private async Task<McpToolResult> ExecuteGitHubCreatePrAsync(Dictionary<string, object> parameters)
    {
        if (_githubClient == null)
        {
            return new McpToolResult { IsError = true, Content = "GitHub client not configured" };
        }

        string title = parameters["title"].ToString() ?? string.Empty;
        string description = parameters["description"].ToString() ?? string.Empty;
        string sourceBranch = parameters["sourceBranch"].ToString() ?? string.Empty;
        string targetBranch = parameters.ContainsKey("targetBranch")
            ? parameters["targetBranch"]?.ToString() ?? "main"
            : "main";

        var result = await _githubClient.CreatePullRequestAsync(title, description, sourceBranch, targetBranch);

        return result.Match(
            success => new McpToolResult
            {
                IsError = false,
                Content = JsonSerializer.Serialize(new
                {
                    number = success.Number,
                    url = success.Url,
                    title = success.Title,
                    state = success.State
                })
            },
            error => new McpToolResult { IsError = true, Content = error });
    }

    private async Task<McpToolResult> ExecuteGitHubPushChangesAsync(Dictionary<string, object> parameters)
    {
        if (_githubClient == null)
        {
            return new McpToolResult { IsError = true, Content = "GitHub client not configured" };
        }

        string branchName = parameters["branchName"].ToString() ?? string.Empty;
        string commitMessage = parameters["commitMessage"].ToString() ?? string.Empty;

        // Parse file changes from parameters
        var changes = new List<FileChange>();
        if (parameters.ContainsKey("changes") && parameters["changes"] is JsonElement changesElement)
        {
            foreach (var changeElement in changesElement.EnumerateArray())
            {
                changes.Add(new FileChange
                {
                    Path = changeElement.GetProperty("path").GetString() ?? string.Empty,
                    Content = changeElement.GetProperty("content").GetString() ?? string.Empty,
                    ChangeType = Enum.Parse<FileChangeType>(
                        changeElement.GetProperty("changeType").GetString() ?? "Update",
                        ignoreCase: true)
                });
            }
        }

        var result = await _githubClient.PushChangesAsync(branchName, changes, commitMessage);

        return result.Match(
            success => new McpToolResult
            {
                IsError = false,
                Content = JsonSerializer.Serialize(new
                {
                    sha = success.Sha,
                    message = success.Message,
                    url = success.Url
                })
            },
            error => new McpToolResult { IsError = true, Content = error });
    }

    private async Task<McpToolResult> ExecuteGitHubCreateIssueAsync(Dictionary<string, object> parameters)
    {
        if (_githubClient == null)
        {
            return new McpToolResult { IsError = true, Content = "GitHub client not configured" };
        }

        string title = parameters["title"].ToString() ?? string.Empty;
        string description = parameters["description"].ToString() ?? string.Empty;
        List<string>? labels = ExtractStringList(parameters, "labels");

        var result = await _githubClient.CreateIssueAsync(title, description, labels);

        return result.Match(
            success => new McpToolResult
            {
                IsError = false,
                Content = JsonSerializer.Serialize(new
                {
                    number = success.Number,
                    url = success.Url,
                    title = success.Title
                })
            },
            error => new McpToolResult { IsError = true, Content = error });
    }

    private async Task<McpToolResult> ExecuteGitHubReadFileAsync(Dictionary<string, object> parameters)
    {
        if (_githubClient == null)
        {
            return new McpToolResult { IsError = true, Content = "GitHub client not configured" };
        }

        string path = parameters["path"].ToString() ?? string.Empty;
        string? branch = parameters.ContainsKey("branch") ? parameters["branch"]?.ToString() : null;

        var result = await _githubClient.ReadFileAsync(path, branch);

        return result.Match(
            success => new McpToolResult
            {
                IsError = false,
                Content = JsonSerializer.Serialize(new
                {
                    path = success.Path,
                    content = success.Content,
                    size = success.Size
                })
            },
            error => new McpToolResult { IsError = true, Content = error });
    }

    private async Task<McpToolResult> ExecuteGitHubListFilesAsync(Dictionary<string, object> parameters)
    {
        if (_githubClient == null)
        {
            return new McpToolResult { IsError = true, Content = "GitHub client not configured" };
        }

        string path = parameters.ContainsKey("path") ? parameters["path"]?.ToString() ?? string.Empty : string.Empty;
        string? branch = parameters.ContainsKey("branch") ? parameters["branch"]?.ToString() : null;

        var result = await _githubClient.ListFilesAsync(path, branch);

        return result.Match(
            success => new McpToolResult
            {
                IsError = false,
                Content = JsonSerializer.Serialize(new { files = success })
            },
            error => new McpToolResult { IsError = true, Content = error });
    }

    private async Task<McpToolResult> ExecuteGitHubCreateBranchAsync(Dictionary<string, object> parameters)
    {
        if (_githubClient == null)
        {
            return new McpToolResult { IsError = true, Content = "GitHub client not configured" };
        }

        string branchName = parameters["branchName"].ToString() ?? string.Empty;
        string baseBranch = parameters.ContainsKey("baseBranch")
            ? parameters["baseBranch"]?.ToString() ?? "main"
            : "main";

        var result = await _githubClient.CreateBranchAsync(branchName, baseBranch);

        return result.Match(
            success => new McpToolResult
            {
                IsError = false,
                Content = JsonSerializer.Serialize(new
                {
                    name = success.Name,
                    sha = success.Sha
                })
            },
            error => new McpToolResult { IsError = true, Content = error });
    }

    private async Task<McpToolResult> ExecuteGitHubSearchCodeAsync(Dictionary<string, object> parameters)
    {
        if (_githubClient == null)
        {
            return new McpToolResult { IsError = true, Content = "GitHub client not configured" };
        }

        string query = parameters["query"].ToString() ?? string.Empty;
        string? path = parameters.ContainsKey("path") ? parameters["path"]?.ToString() : null;

        var result = await _githubClient.SearchCodeAsync(query, path);

        return result.Match(
            success => new McpToolResult
            {
                IsError = false,
                Content = JsonSerializer.Serialize(new { results = success })
            },
            error => new McpToolResult { IsError = true, Content = error });
    }
}
