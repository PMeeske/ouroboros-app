#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using System.Text.Json;
using Ouroboros.Application.GitHub;
using Ouroboros.Application.Mcp;

namespace Ouroboros.Application.CodeGeneration;

/// <summary>
/// Model Context Protocol (MCP) server for code generation and analysis.
/// Provides a standard protocol interface for AI assistants to interact with code tools.
/// </summary>
public partial class McpServer
{
    private readonly RoslynCodeTool _codeTool;
    private readonly DslAssistant _dslAssistant;
    private readonly IGitHubMcpClient? _githubClient;

    public McpServer(RoslynCodeTool codeTool, DslAssistant dslAssistant)
    {
        _codeTool = codeTool;
        _dslAssistant = dslAssistant;
        _githubClient = null;
    }

    public McpServer(RoslynCodeTool codeTool, DslAssistant dslAssistant, IGitHubMcpClient githubClient)
    {
        _codeTool = codeTool;
        _dslAssistant = dslAssistant;
        _githubClient = githubClient;
    }

    /// <summary>
    /// Lists available tools/capabilities.
    /// </summary>
    public McpResponse ListTools()
    {
        List<McpTool> tools = new List<McpTool>
        {
            new McpTool
            {
                Name = "analyze_code",
                Description = "Analyze C# code using Roslyn and custom analyzers",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        code = new { type = "string", description = "C# code to analyze" },
                        runAnalyzers = new { type = "boolean", description = "Run custom analyzers" }
                    },
                    required = new[] { "code" }
                }
            },
            new McpTool
            {
                Name = "create_class",
                Description = "Generate a new C# class with specified structure",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        className = new { type = "string" },
                        namespaceName = new { type = "string" },
                        methods = new { type = "array", items = new { type = "string" } },
                        properties = new { type = "array", items = new { type = "string" } },
                        baseClass = new { type = "string" },
                        interfaces = new { type = "array", items = new { type = "string" } }
                    },
                    required = new[] { "className", "namespaceName" }
                }
            },
            new McpTool
            {
                Name = "add_method",
                Description = "Add a method to an existing class",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        code = new { type = "string" },
                        className = new { type = "string" },
                        methodSignature = new { type = "string" },
                        methodBody = new { type = "string" }
                    },
                    required = new[] { "code", "className", "methodSignature" }
                }
            },
            new McpTool
            {
                Name = "rename_symbol",
                Description = "Rename a symbol (class, method, variable) throughout the code",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        code = new { type = "string" },
                        oldName = new { type = "string" },
                        newName = new { type = "string" }
                    },
                    required = new[] { "code", "oldName", "newName" }
                }
            },
            new McpTool
            {
                Name = "extract_method",
                Description = "Extract code into a new method (refactoring)",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        code = new { type = "string" },
                        startLine = new { type = "integer" },
                        endLine = new { type = "integer" },
                        newMethodName = new { type = "string" }
                    },
                    required = new[] { "code", "startLine", "endLine", "newMethodName" }
                }
            },
            new McpTool
            {
                Name = "suggest_dsl_step",
                Description = "Suggest next DSL pipeline step based on context",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        currentDsl = new { type = "string", description = "Current DSL pipeline" },
                        maxSuggestions = new { type = "integer", description = "Max suggestions to return" }
                    },
                    required = new[] { "currentDsl" }
                }
            },
            new McpTool
            {
                Name = "complete_token",
                Description = "Complete partial DSL token",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        partialToken = new { type = "string" },
                        maxCompletions = new { type = "integer" }
                    },
                    required = new[] { "partialToken" }
                }
            },
            new McpTool
            {
                Name = "validate_dsl",
                Description = "Validate DSL syntax and suggest fixes",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        dsl = new { type = "string", description = "DSL to validate" }
                    },
                    required = new[] { "dsl" }
                }
            },
            new McpTool
            {
                Name = "explain_dsl",
                Description = "Explain what a DSL pipeline does in natural language",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        dsl = new { type = "string" }
                    },
                    required = new[] { "dsl" }
                }
            },
            new McpTool
            {
                Name = "build_dsl",
                Description = "Build a DSL pipeline from high-level goal",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        goal = new { type = "string", description = "High-level goal for the pipeline" }
                    },
                    required = new[] { "goal" }
                }
            }
        };

        // Add GitHub tools if client is available
        if (_githubClient != null)
        {
            tools.AddRange(GetGitHubToolDefinitions());
        }

        return new McpResponse
        {
            Tools = tools
        };
    }

    /// <summary>
    /// Executes a tool with given parameters.
    /// </summary>
    public async Task<McpToolResult> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters)
    {
        try
        {
            return toolName switch
            {
                "analyze_code" => await ExecuteAnalyzeCodeAsync(parameters),
                "create_class" => ExecuteCreateClass(parameters),
                "add_method" => ExecuteAddMethod(parameters),
                "rename_symbol" => ExecuteRenameSymbol(parameters),
                "extract_method" => ExecuteExtractMethod(parameters),
                "suggest_dsl_step" => await ExecuteSuggestDslStepAsync(parameters),
                "complete_token" => ExecuteCompleteToken(parameters),
                "validate_dsl" => await ExecuteValidateDslAsync(parameters),
                "explain_dsl" => await ExecuteExplainDslAsync(parameters),
                "build_dsl" => await ExecuteBuildDslAsync(parameters),
                "github_create_pr" => await ExecuteGitHubCreatePrAsync(parameters),
                "github_push_changes" => await ExecuteGitHubPushChangesAsync(parameters),
                "github_create_issue" => await ExecuteGitHubCreateIssueAsync(parameters),
                "github_read_file" => await ExecuteGitHubReadFileAsync(parameters),
                "github_list_files" => await ExecuteGitHubListFilesAsync(parameters),
                "github_create_branch" => await ExecuteGitHubCreateBranchAsync(parameters),
                "github_search_code" => await ExecuteGitHubSearchCodeAsync(parameters),
                _ => new McpToolResult
                {
                    IsError = true,
                    Content = $"Unknown tool: {toolName}"
                }
            };
        }
        catch (Exception ex)
        {
            return new McpToolResult
            {
                IsError = true,
                Content = $"Tool execution failed: {ex.Message}"
            };
        }
    }

    // Helper methods

    private List<string>? ExtractStringList(Dictionary<string, object> parameters, string key)
    {
        if (!parameters.ContainsKey(key))
            return null;

        object? value = parameters[key];
        if (value == null)
            return null;

        if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
        {
            return jsonElement.EnumerateArray()
                .Select(e => e.GetString() ?? string.Empty)
                .ToList();
        }

        if (value is IEnumerable<object> enumerable)
        {
            return enumerable.Select(o => o?.ToString() ?? string.Empty).ToList();
        }

        return null;
    }
}
