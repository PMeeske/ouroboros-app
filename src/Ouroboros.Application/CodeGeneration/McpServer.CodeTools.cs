#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using System.Text.Json;
using Ouroboros.Application.Mcp;

namespace Ouroboros.Application.CodeGeneration;

/// <summary>
/// Code tool and DSL tool execution methods for the MCP server.
/// </summary>
public partial class McpServer
{
    private async Task<McpToolResult> ExecuteAnalyzeCodeAsync(Dictionary<string, object> parameters)
    {
        string code = parameters["code"].ToString() ?? string.Empty;
        bool runAnalyzers = parameters.ContainsKey("runAnalyzers") && Convert.ToBoolean(parameters["runAnalyzers"]);

        Result<CodeAnalysisResult, string> result = await _codeTool.AnalyzeCodeAsync(code, runAnalyzers: runAnalyzers);

        return result.Match(
            success => new McpToolResult
            {
                IsError = false,
                Content = JsonSerializer.Serialize(new
                {
                    isValid = success.IsValid,
                    diagnostics = success.Diagnostics,
                    classes = success.Classes,
                    methods = success.Methods,
                    usings = success.Usings,
                    analyzerResults = success.AnalyzerResults
                })
            },
            error => new McpToolResult { IsError = true, Content = error });
    }

    private McpToolResult ExecuteCreateClass(Dictionary<string, object> parameters)
    {
        string className = parameters["className"].ToString() ?? string.Empty;
        string namespaceName = parameters["namespaceName"].ToString() ?? string.Empty;
        List<string>? methods = ExtractStringList(parameters, "methods");
        List<string>? properties = ExtractStringList(parameters, "properties");
        List<string>? interfaces = ExtractStringList(parameters, "interfaces");
        string? baseClass = parameters.ContainsKey("baseClass") ? parameters["baseClass"]?.ToString() : null;

        Result<string, string> result = _codeTool.CreateClass(
            className,
            namespaceName,
            methods,
            properties,
            baseClass: baseClass,
            interfaces: interfaces);

        return result.Match(
            success => new McpToolResult { IsError = false, Content = JsonSerializer.Serialize(new { code = success }) },
            error => new McpToolResult { IsError = true, Content = error });
    }

    private McpToolResult ExecuteAddMethod(Dictionary<string, object> parameters)
    {
        string code = parameters["code"].ToString() ?? string.Empty;
        string className = parameters["className"].ToString() ?? string.Empty;
        string methodSignature = parameters["methodSignature"].ToString() ?? string.Empty;
        string? methodBody = parameters.ContainsKey("methodBody") ? parameters["methodBody"]?.ToString() : null;

        Result<string, string> result = _codeTool.AddMethodToClass(code, className, methodSignature, methodBody);

        return result.Match(
            success => new McpToolResult { IsError = false, Content = JsonSerializer.Serialize(new { code = success }) },
            error => new McpToolResult { IsError = true, Content = error });
    }

    private McpToolResult ExecuteRenameSymbol(Dictionary<string, object> parameters)
    {
        string code = parameters["code"].ToString() ?? string.Empty;
        string oldName = parameters["oldName"].ToString() ?? string.Empty;
        string newName = parameters["newName"].ToString() ?? string.Empty;

        Result<string, string> result = _codeTool.RenameSymbol(code, oldName, newName);

        return result.Match(
            success => new McpToolResult { IsError = false, Content = JsonSerializer.Serialize(new { code = success }) },
            error => new McpToolResult { IsError = true, Content = error });
    }

    private McpToolResult ExecuteExtractMethod(Dictionary<string, object> parameters)
    {
        string code = parameters["code"].ToString() ?? string.Empty;
        int startLine = Convert.ToInt32(parameters["startLine"]);
        int endLine = Convert.ToInt32(parameters["endLine"]);
        string newMethodName = parameters["newMethodName"].ToString() ?? string.Empty;

        Result<string, string> result = _codeTool.ExtractMethod(code, startLine, endLine, newMethodName);

        return result.Match(
            success => new McpToolResult { IsError = false, Content = JsonSerializer.Serialize(new { code = success }) },
            error => new McpToolResult { IsError = true, Content = error });
    }

    private async Task<McpToolResult> ExecuteSuggestDslStepAsync(Dictionary<string, object> parameters)
    {
        string currentDsl = parameters["currentDsl"].ToString() ?? string.Empty;
        int maxSuggestions = parameters.ContainsKey("maxSuggestions")
            ? Convert.ToInt32(parameters["maxSuggestions"])
            : 5;

        Result<List<DslSuggestion>, string> result = await _dslAssistant.SuggestNextStepAsync(
            currentDsl,
            maxSuggestions: maxSuggestions);

        return result.Match(
            success => new McpToolResult
            {
                IsError = false,
                Content = JsonSerializer.Serialize(new { suggestions = success })
            },
            error => new McpToolResult { IsError = true, Content = error });
    }

    private McpToolResult ExecuteCompleteToken(Dictionary<string, object> parameters)
    {
        string partialToken = parameters["partialToken"].ToString() ?? string.Empty;
        int maxCompletions = parameters.ContainsKey("maxCompletions")
            ? Convert.ToInt32(parameters["maxCompletions"])
            : 10;

        Result<List<string>, string> result = _dslAssistant.CompleteToken(partialToken, maxCompletions);

        return result.Match(
            success => new McpToolResult { IsError = false, Content = JsonSerializer.Serialize(new { completions = success }) },
            error => new McpToolResult { IsError = true, Content = error });
    }

    private async Task<McpToolResult> ExecuteValidateDslAsync(Dictionary<string, object> parameters)
    {
        string dsl = parameters["dsl"].ToString() ?? string.Empty;

        Result<DslValidationResult, string> result = await _dslAssistant.ValidateAndFixAsync(dsl);

        return result.Match(
            success => new McpToolResult
            {
                IsError = false,
                Content = JsonSerializer.Serialize(new
                {
                    isValid = success.IsValid,
                    errors = success.Errors,
                    warnings = success.Warnings,
                    suggestions = success.Suggestions,
                    fixedDsl = success.FixedDsl
                })
            },
            error => new McpToolResult { IsError = true, Content = error });
    }

    private async Task<McpToolResult> ExecuteExplainDslAsync(Dictionary<string, object> parameters)
    {
        string dsl = parameters["dsl"].ToString() ?? string.Empty;

        Result<string, string> result = await _dslAssistant.ExplainDslAsync(dsl);

        return result.Match(
            success => new McpToolResult { IsError = false, Content = JsonSerializer.Serialize(new { explanation = success }) },
            error => new McpToolResult { IsError = true, Content = error });
    }

    private async Task<McpToolResult> ExecuteBuildDslAsync(Dictionary<string, object> parameters)
    {
        string goal = parameters["goal"].ToString() ?? string.Empty;

        Result<string, string> result = await _dslAssistant.BuildDslInteractivelyAsync(goal);

        return result.Match(
            success => new McpToolResult { IsError = false, Content = JsonSerializer.Serialize(new { dsl = success }) },
            error => new McpToolResult { IsError = true, Content = error });
    }
}
