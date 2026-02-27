// <copyright file="GitHubMcpClient.Queries.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;

namespace Ouroboros.Application.GitHub;

/// <summary>
/// Partial class containing read/query operations for the GitHub MCP client:
/// ReadFile, ListFiles, CreateBranch, GetPullRequestStatus, SearchCode.
/// </summary>
public sealed partial class GitHubMcpClient
{
    /// <inheritdoc/>
    public async Task<Result<FileContent, string>> ReadFileAsync(
        string path,
        string? branch = null,
        CancellationToken ct = default)
    {
        try
        {
            var url = $"/repos/{_options.Owner}/{_options.Repository}/contents/{path}";
            if (!string.IsNullOrWhiteSpace(branch))
            {
                url += $"?ref={branch}";
            }

            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                return Result<FileContent, string>.Failure(
                    $"Failed to read file: {response.StatusCode}");
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var encoding = root.GetProperty("encoding").GetString();
            var contentEncoded = root.GetProperty("content").GetString() ?? string.Empty;

            string decodedContent;
            if (encoding == "base64")
            {
                var bytes = Convert.FromBase64String(contentEncoded.Replace("\n", string.Empty));
                decodedContent = Encoding.UTF8.GetString(bytes);
            }
            else
            {
                decodedContent = contentEncoded;
            }

            var fileContent = new FileContent
            {
                Path = root.GetProperty("path").GetString() ?? string.Empty,
                Content = decodedContent,
                Size = root.GetProperty("size").GetInt32(),
                Sha = root.GetProperty("sha").GetString() ?? string.Empty
            };

            return Result<FileContent, string>.Success(fileContent);
        }
        catch (Exception ex)
        {
            return Result<FileContent, string>.Failure($"Exception reading file: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<GitHubFileInfo>, string>> ListFilesAsync(
        string path = "",
        string? branch = null,
        CancellationToken ct = default)
    {
        try
        {
            var url = $"/repos/{_options.Owner}/{_options.Repository}/contents/{path}";
            if (!string.IsNullOrWhiteSpace(branch))
            {
                url += $"?ref={branch}";
            }

            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                return Result<IReadOnlyList<GitHubFileInfo>, string>.Failure(
                    $"Failed to list files: {response.StatusCode}");
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseBody);

            var fileInfos = new List<GitHubFileInfo>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                fileInfos.Add(new GitHubFileInfo
                {
                    Name = item.GetProperty("name").GetString() ?? string.Empty,
                    Path = item.GetProperty("path").GetString() ?? string.Empty,
                    Type = item.GetProperty("type").GetString() ?? string.Empty,
                    Size = item.TryGetProperty("size", out var sizeElement) ? sizeElement.GetInt32() : (int?)null
                });
            }

            return Result<IReadOnlyList<GitHubFileInfo>, string>.Success(fileInfos);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<GitHubFileInfo>, string>.Failure($"Exception listing files: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<BranchInfo, string>> CreateBranchAsync(
        string branchName,
        string baseBranch = "main",
        CancellationToken ct = default)
    {
        try
        {
            // Get the base branch SHA
            var baseRefResponse = await _httpClient.GetAsync(
                $"/repos/{_options.Owner}/{_options.Repository}/git/ref/heads/{baseBranch}",
                ct);

            if (!baseRefResponse.IsSuccessStatusCode)
            {
                return Result<BranchInfo, string>.Failure(
                    $"Failed to get base branch: {baseRefResponse.StatusCode}");
            }

            var baseRefBody = await baseRefResponse.Content.ReadAsStringAsync(ct);
            using var baseDoc = JsonDocument.Parse(baseRefBody);
            var baseSha = baseDoc.RootElement.GetProperty("object").GetProperty("sha").GetString();

            // Create the new branch
            var payload = new
            {
                @ref = $"refs/heads/{branchName}",
                sha = baseSha
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(
                $"/repos/{_options.Owner}/{_options.Repository}/git/refs",
                content,
                ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                return Result<BranchInfo, string>.Failure(
                    $"Failed to create branch: {response.StatusCode} - {error}");
            }

            var branchInfo = new BranchInfo
            {
                Name = branchName,
                Sha = baseSha ?? string.Empty,
                IsProtected = false
            };

            return Result<BranchInfo, string>.Success(branchInfo);
        }
        catch (Exception ex)
        {
            return Result<BranchInfo, string>.Failure($"Exception creating branch: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<PullRequestStatus, string>> GetPullRequestStatusAsync(
        int pullRequestNumber,
        CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/repos/{_options.Owner}/{_options.Repository}/pulls/{pullRequestNumber}",
                ct);

            if (!response.IsSuccessStatusCode)
            {
                return Result<PullRequestStatus, string>.Failure(
                    $"Failed to get pull request status: {response.StatusCode}");
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var status = new PullRequestStatus
            {
                Number = root.GetProperty("number").GetInt32(),
                State = root.GetProperty("state").GetString() ?? string.Empty,
                Mergeable = root.TryGetProperty("mergeable", out var mergeableElement)
                    ? mergeableElement.GetBoolean()
                    : (bool?)null,
                Additions = root.GetProperty("additions").GetInt32(),
                Deletions = root.GetProperty("deletions").GetInt32(),
                ChangedFiles = root.GetProperty("changed_files").GetInt32()
            };

            return Result<PullRequestStatus, string>.Success(status);
        }
        catch (Exception ex)
        {
            return Result<PullRequestStatus, string>.Failure($"Exception getting pull request status: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<CodeSearchResult>, string>> SearchCodeAsync(
        string query,
        string? path = null,
        CancellationToken ct = default)
    {
        try
        {
            var searchQuery = $"{query} repo:{_options.Owner}/{_options.Repository}";
            if (!string.IsNullOrWhiteSpace(path))
            {
                searchQuery += $" path:{path}";
            }

            var encodedQuery = Uri.EscapeDataString(searchQuery);
            var response = await _httpClient.GetAsync($"/search/code?q={encodedQuery}", ct);

            if (!response.IsSuccessStatusCode)
            {
                return Result<IReadOnlyList<CodeSearchResult>, string>.Failure(
                    $"Failed to search code: {response.StatusCode}");
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseBody);
            var items = doc.RootElement.GetProperty("items");

            var results = new List<CodeSearchResult>();
            foreach (var item in items.EnumerateArray())
            {
                results.Add(new CodeSearchResult
                {
                    Path = item.GetProperty("path").GetString() ?? string.Empty,
                    Filename = item.GetProperty("name").GetString() ?? string.Empty,
                    MatchedContent = item.TryGetProperty("text_matches", out var matches) && matches.GetArrayLength() > 0
                        ? matches[0].GetProperty("fragment").GetString() ?? string.Empty
                        : string.Empty,
                    LineNumber = null
                });
            }

            return Result<IReadOnlyList<CodeSearchResult>, string>.Success(results);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<CodeSearchResult>, string>.Failure($"Exception searching code: {ex.Message}");
        }
    }
}
