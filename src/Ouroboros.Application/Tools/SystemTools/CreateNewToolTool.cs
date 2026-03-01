// <copyright file="CreateNewToolTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.SystemTools;

using System.Text.Json;

/// <summary>
/// Create a new tool at runtime by generating source code.
/// </summary>
internal class CreateNewToolTool : ITool
{
    public string Name => "create_new_tool";
    public string Description => "Create a new tool by writing a new C# class file. Input JSON: {\"name\": \"tool_name\", \"description\": \"what the tool does\", \"implementation\": \"the tool logic as C# code\"}. I will generate the full ITool class.";
    public string? JsonSchema => """{"type":"object","properties":{"name":{"type":"string"},"description":{"type":"string"},"implementation":{"type":"string"}},"required":["name","description","implementation"]}""";

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            var args = JsonSerializer.Deserialize<JsonElement>(input);
            var name = args.GetProperty("name").GetString() ?? "";
            var description = args.GetProperty("description").GetString() ?? "";
            var implementation = args.GetProperty("implementation").GetString() ?? "";

            if (string.IsNullOrWhiteSpace(name))
            {
                return Result<string, string>.Failure("Tool name is required.");
            }

            // Convert snake_case to PascalCase for class name
            var className = string.Join("", name.Split('_').Select(s =>
                char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant())) + "Tool";

            var code = $@"// Auto-generated tool: {name}
// Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
using System;
using System.Threading;
using System.Threading.Tasks;
using Ouroboros.Core.Functional;
using Ouroboros.Pipeline.Tools;

namespace Ouroboros.Application.Tools.Generated;

/// <summary>
/// {description}
/// </summary>
public class {className} : ITool
{{
    public string Name => ""{name}"";
    public string Description => ""{description.Replace("\"", "\\\"")}"";
    public string? JsonSchema => null;

    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {{
        try
        {{
            {implementation}
        }}
        catch (Exception ex) when (ex is not OperationCanceledException)
        {{
            return Result<string, string>.Failure($""Tool execution failed: {{ex.Message}}"");
        }}
    }}
}}
";

            // Ensure directory exists
            var toolsDir = Path.Combine(Environment.CurrentDirectory, "src", "Ouroboros.Application", "Tools", "Generated");
            Directory.CreateDirectory(toolsDir);

            var filePath = Path.Combine(toolsDir, $"{className}.cs");
            await File.WriteAllTextAsync(filePath, code, ct);

            return Result<string, string>.Success($@"Created new tool: **{name}**

File: `src/Ouroboros.Application/Tools/Generated/{className}.cs`

To use this tool:
1. Run `dotnet build` to compile
2. Register in SystemAccessTools.CreateAllTools() or dynamically load

```csharp
{code.Substring(0, Math.Min(500, code.Length))}...
```");
        }
        catch (IOException ex)
        {
            return Result<string, string>.Failure($"Tool creation failed: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return Result<string, string>.Failure($"Tool creation failed: {ex.Message}");
        }
    }
}
