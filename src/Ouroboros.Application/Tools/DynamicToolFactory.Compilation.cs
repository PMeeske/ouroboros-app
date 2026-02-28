// <copyright file="DynamicToolFactory.Compilation.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Ouroboros.Tools;

/// <summary>
/// Code generation, compilation, and reference building for DynamicToolFactory.
/// </summary>
public partial class DynamicToolFactory
{
    private async Task<string> GenerateToolCodeAsync(
        string className,
        string toolName,
        string description,
        CancellationToken ct)
    {
        string prompt = $@"Generate a C# class that implements the ITool interface for the following tool:

Tool Name: {toolName}
Description: {description}

Requirements:
1. Class name: {className}
2. Implement the ITool interface with these members:
   - string Name {{ get; }} => ""{toolName}""
   - string Description {{ get; }} => ""{description}""
   - string? JsonSchema {{ get; }} => null or a valid JSON schema for args
   - Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct)

3. Use HttpClient for any web requests
4. Handle errors gracefully and return Result<string, string>.Failure() on error
5. Return Result<string, string>.Success() with the result string on success

The Result type is: Result<TSuccess, TError> with static methods:
- Result<string, string>.Success(string value)
- Result<string, string>.Failure(string error)

IMPORTANT: You MUST include these exact using statements at the top:

```csharp
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ouroboros.Core.Monads;
using Ouroboros.Tools;

namespace Ouroboros.DynamicTools
{{
    public class {className} : ITool
    {{
        public string Name => ""{toolName}"";
        public string Description => ""{description}"";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {{
            try
            {{
                // Your implementation here
                return Result<string, string>.Success(""Result"");
            }}
            catch (InvalidOperationException ex)
            {{
                return Result<string, string>.Failure(ex.Message);
            }}
        }}
    }}
}}
```

Generate ONLY the complete C# code, no explanations.";

        return await _llm.InnerModel.GenerateTextAsync(prompt, ct);
    }

    private Result<ITool?, string> CompileTool(string code, string className)
    {
        try
        {
            // Parse the code
            var syntaxTree = CSharpSyntaxTree.ParseText(code);

            // Create compilation
            var assemblyName = $"DynamicTool_{_toolCounter}_{Guid.NewGuid():N}";
            var compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { syntaxTree },
                _references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithOptimizationLevel(OptimizationLevel.Release)
                    .WithPlatform(Platform.AnyCpu));

            // Emit to memory stream
            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);

            if (!emitResult.Success)
            {
                var errors = emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.GetMessage())
                    .ToList();
                return Result<ITool?, string>.Failure(string.Join("\n", errors));
            }

            // Load assembly and create instance
            ms.Seek(0, SeekOrigin.Begin);
            var assembly = _loadContext.LoadFromStream(ms);

            // Find the tool type
            var toolType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(ITool).IsAssignableFrom(t) && !t.IsAbstract);

            if (toolType == null)
            {
                return Result<ITool?, string>.Failure($"No ITool implementation found in compiled code");
            }

            var tool = (ITool?)Activator.CreateInstance(toolType);
            return tool != null
                ? Result<ITool?, string>.Success(tool)
                : Result<ITool?, string>.Failure("Failed to create tool instance");
        }
        catch (OperationCanceledException) { throw; }
        catch (InvalidOperationException ex)
        {
            return Result<ITool?, string>.Failure($"Compilation error: {ex.Message}");
        }
    }

    private List<MetadataReference> BuildReferences()
    {
        var refs = new List<MetadataReference>();

        // Get runtime assemblies
        var assemblies = new[]
        {
            typeof(object).Assembly,                           // System.Private.CoreLib
            typeof(Console).Assembly,                          // System.Console
            typeof(System.Net.Http.HttpClient).Assembly,       // System.Net.Http
            typeof(Uri).Assembly,                              // System.Private.Uri
            typeof(Task).Assembly,                             // System.Threading.Tasks
            typeof(Enumerable).Assembly,                       // System.Linq
            typeof(JsonSerializer).Assembly,                   // System.Text.Json
            typeof(ITool).Assembly,                            // Ouroboros.Tools
            typeof(Result<,>).Assembly,                        // Ouroboros.Core (Result type)
            Assembly.Load("System.Runtime"),
            Assembly.Load("System.Collections"),
            Assembly.Load("netstandard"),
        };

        foreach (var asm in assemblies)
        {
            if (!string.IsNullOrEmpty(asm.Location))
            {
                refs.Add(MetadataReference.CreateFromFile(asm.Location));
            }
        }

        // Add additional runtime references
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var additionalRefs = new[]
        {
            "System.Threading.dll",
            "System.Threading.Tasks.dll",
            "System.Net.Primitives.dll",
            "System.Runtime.Extensions.dll",
            "System.ComponentModel.Primitives.dll",
        };

        foreach (var refName in additionalRefs)
        {
            var refPath = Path.Combine(runtimeDir, refName);
            if (File.Exists(refPath))
            {
                refs.Add(MetadataReference.CreateFromFile(refPath));
            }
        }

        return refs;
    }

    private static string SanitizeToolName(string name)
    {
        // Convert to snake_case and remove invalid chars
        var sb = new StringBuilder();
        foreach (char c in name.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
            else if (c == ' ' || c == '-' || c == '_')
                sb.Append('_');
        }
        return sb.ToString().Trim('_');
    }

    private static string ToPascalCase(string snakeCase)
    {
        return string.Join("", snakeCase.Split('_')
            .Where(s => s.Length > 0)
            .Select(s => char.ToUpperInvariant(s[0]) + s[1..]));
    }
}
