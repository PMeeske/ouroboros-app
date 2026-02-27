// <copyright file="GitHubMcpClient.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Ouroboros.Application.GitHub;

/// <summary>
/// Implementation of GitHub MCP client using GitHub REST API.
/// Contains constructor and write operations: CreatePullRequest, PushChanges, CreateIssue.
/// </summary>
public sealed partial class GitHubMcpClient : IGitHubMcpClient
{
    private static readonly JsonSerializerOptions GitHubJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
    };

    private readonly GitHubMcpClientOptions _options;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubMcpClient"/> class.
    /// </summary>
    /// <param name="options">The configuration options.</param>
    /// <param name="httpClient">Optional HTTP client (will create one if not provided).</param>
    public GitHubMcpClient(GitHubMcpClientOptions options, HttpClient? httpClient = null)
    {
        if (!options.IsValid())
        {
            throw new ArgumentException("Invalid GitHubMcpClientOptions", nameof(options));
        }

        _options = options;
        _httpClient = httpClient ?? new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        });
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        // GitHub API authentication - supports both Personal Access Tokens (PAT) and OAuth tokens
        // For PATs, use scheme "token"; for OAuth tokens, use "Bearer"
        var authScheme = _options.Token.StartsWith("gh") ? "token" : "Bearer";
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(authScheme, _options.Token);
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Ouroboros", "1.0"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _httpClient.Timeout = _options.Timeout;

        _jsonOptions = GitHubJsonOptions;
    }

    /// <inheritdoc/>
    public async Task<Result<PullRequestInfo, string>> CreatePullRequestAsync(
        string title,
        string description,
        string sourceBranch,
        string targetBranch = "main",
        IReadOnlyList<FileChange>? files = null,
        CancellationToken ct = default)
    {
        try
        {
            var payload = new
            {
                title,
                body = description,
                head = sourceBranch,
                @base = targetBranch
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(
                $"/repos/{_options.Owner}/{_options.Repository}/pulls",
                content,
                ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                return Result<PullRequestInfo, string>.Failure(
                    $"Failed to create pull request: {response.StatusCode} - {error}");
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var prInfo = new PullRequestInfo
            {
                Number = root.GetProperty("number").GetInt32(),
                Url = root.GetProperty("html_url").GetString() ?? string.Empty,
                Title = root.GetProperty("title").GetString() ?? string.Empty,
                State = root.GetProperty("state").GetString() ?? string.Empty,
                HeadBranch = root.GetProperty("head").GetProperty("ref").GetString() ?? string.Empty,
                BaseBranch = root.GetProperty("base").GetProperty("ref").GetString() ?? string.Empty,
                CreatedAt = root.GetProperty("created_at").GetDateTime()
            };

            return Result<PullRequestInfo, string>.Success(prInfo);
        }
        catch (Exception ex)
        {
            return Result<PullRequestInfo, string>.Failure($"Exception creating pull request: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<CommitInfo, string>> PushChangesAsync(
        string branchName,
        IReadOnlyList<FileChange> changes,
        string commitMessage,
        CancellationToken ct = default)
    {
        try
        {
            // Get the current branch reference
            var refResponse = await _httpClient.GetAsync(
                $"/repos/{_options.Owner}/{_options.Repository}/git/ref/heads/{branchName}",
                ct);

            if (!refResponse.IsSuccessStatusCode)
            {
                return Result<CommitInfo, string>.Failure(
                    $"Failed to get branch reference: {refResponse.StatusCode}");
            }

            var refBody = await refResponse.Content.ReadAsStringAsync(ct);
            using var refDoc = JsonDocument.Parse(refBody);
            var currentSha = refDoc.RootElement.GetProperty("object").GetProperty("sha").GetString();

            // Create a tree with the file changes
            var treeItems = new List<object>();
            foreach (var change in changes)
            {
                treeItems.Add(new
                {
                    path = change.Path,
                    mode = "100644",
                    type = "blob",
                    content = change.Content
                });
            }

            var treePayload = new
            {
                base_tree = currentSha,
                tree = treeItems
            };

            var treeContent = new StringContent(
                JsonSerializer.Serialize(treePayload, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var treeResponse = await _httpClient.PostAsync(
                $"/repos/{_options.Owner}/{_options.Repository}/git/trees",
                treeContent,
                ct);

            if (!treeResponse.IsSuccessStatusCode)
            {
                return Result<CommitInfo, string>.Failure(
                    $"Failed to create tree: {treeResponse.StatusCode}");
            }

            var treeBody = await treeResponse.Content.ReadAsStringAsync(ct);
            using var treeDoc = JsonDocument.Parse(treeBody);
            var treeSha = treeDoc.RootElement.GetProperty("sha").GetString();

            // Create commit
            var commitPayload = new
            {
                message = commitMessage,
                tree = treeSha,
                parents = new[] { currentSha }
            };

            var commitContent = new StringContent(
                JsonSerializer.Serialize(commitPayload, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var commitResponse = await _httpClient.PostAsync(
                $"/repos/{_options.Owner}/{_options.Repository}/git/commits",
                commitContent,
                ct);

            if (!commitResponse.IsSuccessStatusCode)
            {
                return Result<CommitInfo, string>.Failure(
                    $"Failed to create commit: {commitResponse.StatusCode}");
            }

            var commitBody = await commitResponse.Content.ReadAsStringAsync(ct);
            using var commitDoc = JsonDocument.Parse(commitBody);
            var newCommitSha = commitDoc.RootElement.GetProperty("sha").GetString();

            // Update branch reference
            var updatePayload = new
            {
                sha = newCommitSha,
                force = false
            };

            var updateContent = new StringContent(
                JsonSerializer.Serialize(updatePayload, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var updateResponse = await _httpClient.PatchAsync(
                $"/repos/{_options.Owner}/{_options.Repository}/git/refs/heads/{branchName}",
                updateContent,
                ct);

            if (!updateResponse.IsSuccessStatusCode)
            {
                return Result<CommitInfo, string>.Failure(
                    $"Failed to update branch: {updateResponse.StatusCode}");
            }

            var commitInfo = new CommitInfo
            {
                Sha = newCommitSha ?? string.Empty,
                Message = commitMessage,
                Url = commitDoc.RootElement.GetProperty("html_url").GetString() ?? string.Empty,
                CommittedAt = commitDoc.RootElement.GetProperty("committer").GetProperty("date").GetDateTime()
            };

            return Result<CommitInfo, string>.Success(commitInfo);
        }
        catch (Exception ex)
        {
            return Result<CommitInfo, string>.Failure($"Exception pushing changes: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<IssueInfo, string>> CreateIssueAsync(
        string title,
        string description,
        IReadOnlyList<string>? labels = null,
        CancellationToken ct = default)
    {
        try
        {
            var payload = new
            {
                title,
                body = description,
                labels = labels ?? Array.Empty<string>()
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(
                $"/repos/{_options.Owner}/{_options.Repository}/issues",
                content,
                ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                return Result<IssueInfo, string>.Failure(
                    $"Failed to create issue: {response.StatusCode} - {error}");
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var issueInfo = new IssueInfo
            {
                Number = root.GetProperty("number").GetInt32(),
                Url = root.GetProperty("html_url").GetString() ?? string.Empty,
                Title = root.GetProperty("title").GetString() ?? string.Empty,
                State = root.GetProperty("state").GetString() ?? string.Empty,
                CreatedAt = root.GetProperty("created_at").GetDateTime()
            };

            return Result<IssueInfo, string>.Success(issueInfo);
        }
        catch (Exception ex)
        {
            return Result<IssueInfo, string>.Failure($"Exception creating issue: {ex.Message}");
        }
    }
}
