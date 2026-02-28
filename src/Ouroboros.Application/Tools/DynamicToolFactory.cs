// <copyright file="DynamicToolFactory.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http;

namespace Ouroboros.Application.Tools;

using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ouroboros.Providers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Ouroboros.Application.Mcp;
using Ouroboros.Application.Tools.CaptchaResolver;
using Ouroboros.Tools;

/// <summary>
/// Factory for dynamically generating, compiling, and registering tools at runtime.
/// Uses Roslyn for compilation and LLM for code generation.
/// </summary>
public partial class DynamicToolFactory
{
    private static readonly HttpClient _sharedHttpClient = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        AutomaticDecompression = System.Net.DecompressionMethods.All
    }) { Timeout = TimeSpan.FromSeconds(30) };

    private readonly ToolAwareChatModel _llm;
    private readonly PlaywrightMcpTool? _playwrightMcpTool;
    private readonly CaptchaResolverChain _captchaResolver;
    private readonly AssemblyLoadContext _loadContext;
    private readonly List<MetadataReference> _references;
    private readonly string _storagePath;
    private int _toolCounter;

    /// <summary>
    /// Dangerous namespaces that must not appear in dynamically compiled tool code.
    /// These namespaces allow arbitrary file/process/network/emit access and are blocked
    /// to prevent sandbox escape in LLM-generated tools.
    /// </summary>
    private static readonly string[] BlockedNamespaces =
    [
        "System.IO",
        "System.Diagnostics",
        "System.Net",
        "System.Reflection.Emit",
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicToolFactory"/> class.
    /// </summary>
    /// <param name="llm">The LLM for generating tool code.</param>
    /// <param name="storagePath">Optional path to store generated tools.</param>
    /// <param name="playwrightMcpTool">Optional Playwright tool for browser automation.</param>
    public DynamicToolFactory(ToolAwareChatModel llm, string? storagePath = null, PlaywrightMcpTool? playwrightMcpTool = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _playwrightMcpTool = playwrightMcpTool;
        _loadContext = new AssemblyLoadContext("DynamicTools", isCollectible: true);
        _storagePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".ouroboros",
            "dynamic_tools");

        Directory.CreateDirectory(_storagePath);

        // Build reference assemblies for compilation
        _references = BuildReferences();

        // Initialize CAPTCHA resolver chain with available strategies
        // Use semantic decorator to enhance detection and provide intelligent guidance
        var visionResolver = new VisionCaptchaResolver(_playwrightMcpTool);
        var alternativeResolver = new AlternativeSearchResolver();

        _captchaResolver = new CaptchaResolverChain()
            .AddStrategy(new SemanticCaptchaResolverDecorator(visionResolver, _llm))
            .AddStrategy(new SemanticCaptchaResolverDecorator(alternativeResolver, _llm, useSemanticDetection: false))
            .AddStrategy(visionResolver)  // Fallback without semantic analysis
            .AddStrategy(alternativeResolver);
    }

    /// <summary>
    /// Gets the list of dynamically created tools.
    /// </summary>
    public List<(string Name, string Description, ITool Tool)> CreatedTools { get; } = new();

    /// <summary>
    /// Generates a new tool based on a natural language description.
    /// </summary>
    /// <param name="toolName">The desired name for the tool (e.g., "google_search").</param>
    /// <param name="description">Natural language description of what the tool should do.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the created tool or an error.</returns>
    public async Task<Result<ITool, string>> CreateToolAsync(
        string toolName,
        string description,
        CancellationToken ct = default)
    {
        try
        {
            // Sanitize tool name
            string safeName = SanitizeToolName(toolName);
            string className = $"{ToPascalCase(safeName)}Tool";

            // Generate tool code using LLM
            string codeResult = await GenerateToolCodeAsync(className, safeName, description, ct);
            if (string.IsNullOrWhiteSpace(codeResult))
            {
                return Result<ITool, string>.Failure("LLM failed to generate tool code");
            }

            // Extract code from markdown if present
            string code = ExtractCode(codeResult);

            // Ensure required using statements are present
            code = EnsureRequiredUsings(code);

            // SECURITY: Validate code does not use dangerous namespaces
            var securityViolation = ValidateCodeSecurity(code);
            if (securityViolation != null)
            {
                return Result<ITool, string>.Failure($"Security violation: {securityViolation}");
            }

            // Compile the tool
            var compileResult = CompileTool(code, className);
            if (!compileResult.IsSuccess)
            {
                // Try to fix compilation errors with LLM
                string fixPrompt = $@"The following C# code has compilation errors. Fix them and return ONLY the corrected code.
IMPORTANT: Make sure to include 'using Ouroboros.Core.Monads;' for the Result type.

ERRORS:
{compileResult.Error}

CODE:
```csharp
{code}
```";
                string fixedCode = await _llm.InnerModel.GenerateTextAsync(fixPrompt, ct);
                fixedCode = ExtractCode(fixedCode);
                fixedCode = EnsureRequiredUsings(fixedCode); // Ensure usings are present

                // SECURITY: Re-validate after LLM fix
                securityViolation = ValidateCodeSecurity(fixedCode);
                if (securityViolation != null)
                {
                    return Result<ITool, string>.Failure($"Security violation in fixed code: {securityViolation}");
                }

                compileResult = CompileTool(fixedCode, className);

                if (!compileResult.IsSuccess)
                {
                    return Result<ITool, string>.Failure($"Compilation failed: {compileResult.Error}");
                }

                code = fixedCode;
            }

            // Save the generated code for inspection
            string codePath = Path.Combine(_storagePath, $"{className}.cs");
            await File.WriteAllTextAsync(codePath, code, ct);

            // Create instance of the tool
            if (compileResult.Value is not ITool tool)
            {
                return Result<ITool, string>.Failure("Compiled type does not implement ITool");
            }

            CreatedTools.Add((safeName, description, tool));
            _toolCounter++;

            return Result<ITool, string>.Success(tool);
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            return Result<ITool, string>.Failure($"Tool creation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates that generated code does not use dangerous namespaces.
    /// Returns null if safe, or a description of the violation.
    /// </summary>
    private static string? ValidateCodeSecurity(string code)
    {
        foreach (var ns in BlockedNamespaces)
        {
            // Check for using directives: "using System.IO;" or "using System.IO.Something;"
            if (Regex.IsMatch(code, $@"using\s+{Regex.Escape(ns)}(\s*;|\.\w)", RegexOptions.Multiline))
            {
                return $"Code uses blocked namespace '{ns}'. Dynamic tools may not use {ns} for security reasons.";
            }

            // Check for fully-qualified usage: "System.IO.File.ReadAllText"
            if (code.Contains($"{ns}."))
            {
                return $"Code references blocked namespace '{ns}'. Dynamic tools may not use {ns} for security reasons.";
            }
        }

        return null;
    }

    /// <summary>
    /// Creates a simple delegate-based tool without full code generation.
    /// Faster but less flexible than full code generation.
    /// </summary>
    /// <param name="toolName">The name for the tool.</param>
    /// <param name="description">Description of what the tool does.</param>
    /// <param name="implementation">The implementation function.</param>
    /// <returns>The created tool.</returns>
    public ITool CreateSimpleTool(
        string toolName,
        string description,
        Func<string, Task<string>> implementation)
    {
        var tool = new DelegateTool(toolName, description, implementation);
        CreatedTools.Add((toolName, description, tool));
        return tool;
    }
}
