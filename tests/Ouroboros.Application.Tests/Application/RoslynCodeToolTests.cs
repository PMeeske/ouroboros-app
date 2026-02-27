// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.

using FluentAssertions;
using Ouroboros.Application.CodeGeneration;
using Xunit;

namespace Ouroboros.Tests.Application;

/// <summary>
/// Unit tests for RoslynCodeTool covering code compilation, syntax tree manipulation,
/// diagnostic handling, and code fix application.
/// </summary>
[Trait("Category", "Unit")]
public class RoslynCodeToolTests
{
    private readonly Ouroboros.Application.CodeGeneration.RoslynCodeTool _tool;

    public RoslynCodeToolTests()
    {
        _tool = new Ouroboros.Application.CodeGeneration.RoslynCodeTool();
    }

    // ========================================================================
    // AnalyzeCodeAsync
    // ========================================================================

    [Fact]
    public async Task AnalyzeCodeAsync_ValidCode_ReturnsSuccess()
    {
        // Arrange
        string code = "public class Foo { public int Bar { get; set; } }";

        // Act
        var result = await _tool.AnalyzeCodeAsync(code);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Classes.Should().Contain("Foo");
    }

    [Fact]
    public async Task AnalyzeCodeAsync_ValidCode_ExtractsClasses()
    {
        // Arrange
        string code = @"
namespace TestNs
{
    public class Alpha { }
    public class Beta { }
}";

        // Act
        var result = await _tool.AnalyzeCodeAsync(code);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Classes.Should().Contain("Alpha");
        result.Value.Classes.Should().Contain("Beta");
    }

    [Fact]
    public async Task AnalyzeCodeAsync_ValidCode_ExtractsMethods()
    {
        // Arrange
        string code = @"
public class Foo
{
    public void DoWork() { }
    public int Calculate(int x) { return x; }
}";

        // Act
        var result = await _tool.AnalyzeCodeAsync(code);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Methods.Should().HaveCount(2);
        result.Value.Methods.Should().Contain(m => m.Contains("DoWork"));
        result.Value.Methods.Should().Contain(m => m.Contains("Calculate"));
    }

    [Fact]
    public async Task AnalyzeCodeAsync_ValidCode_ExtractsUsings()
    {
        // Arrange
        string code = @"
using System;
using System.Linq;
public class Foo { }";

        // Act
        var result = await _tool.AnalyzeCodeAsync(code);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Usings.Should().Contain("System");
        result.Value.Usings.Should().Contain("System.Linq");
    }

    [Fact]
    public async Task AnalyzeCodeAsync_WithAnalyzers_DetectsBlockingAsyncCalls()
    {
        // Arrange
        string code = @"
using System.Threading.Tasks;
public class Foo
{
    public void Bad()
    {
        var task = Task.Delay(100);
        task.Wait();
    }
}";

        // Act
        var result = await _tool.AnalyzeCodeAsync(code, runAnalyzers: true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AnalyzerResults.Should().Contain(r => r.Contains("blocking async"));
    }

    [Fact]
    public async Task AnalyzeCodeAsync_WithAnalyzers_DetectsMissingDocumentation()
    {
        // Arrange
        string code = @"
public class Foo
{
    public void Bar() { }
}";

        // Act
        var result = await _tool.AnalyzeCodeAsync(code, runAnalyzers: true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AnalyzerResults.Should().Contain(r => r.Contains("missing XML documentation"));
    }

    [Fact]
    public async Task AnalyzeCodeAsync_WithoutAnalyzers_DoesNotRunCustomAnalyzers()
    {
        // Arrange
        string code = @"public class Foo { public void Bar() { } }";

        // Act
        var result = await _tool.AnalyzeCodeAsync(code, runAnalyzers: false);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AnalyzerResults.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeCodeAsync_InvalidSyntax_ReportsErrors()
    {
        // Arrange
        string code = "public class { }"; // missing class name

        // Act
        var result = await _tool.AnalyzeCodeAsync(code);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsValid.Should().BeFalse();
        result.Value.Diagnostics.Should().NotBeEmpty();
    }

    // ========================================================================
    // CreateClass
    // ========================================================================

    [Fact]
    public void CreateClass_MinimalParams_GeneratesValidClass()
    {
        // Act
        var result = _tool.CreateClass("MyService", "MyApp.Services");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("class MyService");
        result.Value.Should().Contain("MyApp.Services");
    }

    [Fact]
    public void CreateClass_WithMethods_IncludesMethodDeclarations()
    {
        // Act
        var result = _tool.CreateClass(
            "MyService",
            "MyApp.Services",
            methods: new List<string> { "public void Execute()", "public int Count()" });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Execute");
        result.Value.Should().Contain("Count");
    }

    [Fact]
    public void CreateClass_WithProperties_IncludesPropertyDeclarations()
    {
        // Act
        var result = _tool.CreateClass(
            "MyModel",
            "MyApp.Models",
            properties: new List<string> { "string Name", "int Age" });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Name");
        result.Value.Should().Contain("Age");
    }

    [Fact]
    public void CreateClass_WithBaseClass_IncludesInheritance()
    {
        // Act
        var result = _tool.CreateClass(
            "DerivedClass",
            "MyApp",
            baseClass: "BaseClass");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("BaseClass");
    }

    [Fact]
    public void CreateClass_WithInterfaces_IncludesImplementation()
    {
        // Act
        var result = _tool.CreateClass(
            "MyService",
            "MyApp",
            interfaces: new List<string> { "IDisposable", "IService" });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("IDisposable");
        result.Value.Should().Contain("IService");
    }

    [Fact]
    public void CreateClass_WithDocComments_IncludesXmlDocumentation()
    {
        // Act
        var result = _tool.CreateClass(
            "DocumentedClass",
            "MyApp",
            addDocComments: true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("/// <summary>");
    }

    [Fact]
    public void CreateClass_WithoutDocComments_OmitsXmlDocumentation()
    {
        // Act
        var result = _tool.CreateClass(
            "SimpleClass",
            "MyApp",
            addDocComments: false);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotContain("/// <summary>");
    }

    // ========================================================================
    // AddMethodToClass
    // ========================================================================

    [Fact]
    public void AddMethodToClass_ExistingClass_AddsMethod()
    {
        // Arrange
        string code = "public class Foo { }";

        // Act
        var result = _tool.AddMethodToClass(code, "Foo", "public void NewMethod()");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("NewMethod");
    }

    [Fact]
    public void AddMethodToClass_NonexistentClass_ReturnsFailure()
    {
        // Arrange
        string code = "public class Foo { }";

        // Act
        var result = _tool.AddMethodToClass(code, "Bar", "public void NewMethod()");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Bar");
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public void AddMethodToClass_WithBody_IncludesMethodBody()
    {
        // Arrange
        string code = "public class Foo { }";

        // Act
        var result = _tool.AddMethodToClass(
            code, "Foo", "public int GetValue()", "return 42;");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("GetValue");
        result.Value.Should().Contain("return 42");
    }

    // ========================================================================
    // RenameSymbol
    // ========================================================================

    [Fact]
    public void RenameSymbol_SingleOccurrence_Renames()
    {
        // Arrange
        string code = "public class OldName { }";

        // Act
        var result = _tool.RenameSymbol(code, "OldName", "NewName");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("NewName");
        result.Value.Should().NotContain("OldName");
    }

    [Fact]
    public void RenameSymbol_MultipleOccurrences_RenamesAll()
    {
        // Arrange
        string code = @"
public class OldName
{
    public OldName Create() { return new OldName(); }
}";

        // Act
        var result = _tool.RenameSymbol(code, "OldName", "NewName");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotContain("OldName");
    }

    [Fact]
    public void RenameSymbol_NoOccurrence_ReturnsOriginalCode()
    {
        // Arrange
        string code = "public class Foo { }";

        // Act
        var result = _tool.RenameSymbol(code, "NonExistent", "Replaced");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Foo");
    }

    // ========================================================================
    // ExtractMethod
    // ========================================================================

    [Fact]
    public void ExtractMethod_ValidRange_ExtractsStatements()
    {
        // Arrange
        string code = @"public class Foo
{
    public void Bar()
    {
        int x = 1;
        int y = 2;
        int z = x + y;
    }
}";

        // Act - extract lines with statements
        var result = _tool.ExtractMethod(code, 4, 5, "ComputeValues");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("ComputeValues");
    }

    [Fact]
    public void ExtractMethod_NoStatementsInRange_ReturnsFailure()
    {
        // Arrange
        string code = @"public class Foo
{
    public void Bar()
    {
        int x = 1;
    }
}";

        // Act - line range outside statements
        var result = _tool.ExtractMethod(code, 0, 0, "NewMethod");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    // ========================================================================
    // GenerateSourceGenerator
    // ========================================================================

    [Fact]
    public void GenerateSourceGenerator_ValidInput_ProducesCode()
    {
        // Arrange
        string logic = @"string generatedCode = ""public class Generated {}"";";

        // Act
        var result = _tool.GenerateSourceGenerator("MyGenerator", logic);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("MyGenerator");
        result.Value.Should().Contain("[Generator]");
        result.Value.Should().Contain("ISourceGenerator");
    }

    [Fact]
    public void GenerateSourceGenerator_ContainsErrorHandling()
    {
        // Act
        var result = _tool.GenerateSourceGenerator("TestGen", "");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("catch (Exception");
        result.Value.Should().Contain("ReportDiagnostic");
    }

    // ========================================================================
    // ExtractCodeFromMarkdown (tested via GenerateCodeFromDescriptionAsync behavior)
    // ========================================================================

    [Fact]
    public async Task AnalyzeCodeAsync_MonadicAnalyzer_DetectsTaskWithoutResult()
    {
        // Arrange - code that returns Task but not Result<T>
        string code = @"
using System.Threading.Tasks;
public class Foo
{
    public async Task DoWorkAsync() { await Task.Delay(1); }
}";

        // Act
        var result = await _tool.AnalyzeCodeAsync(code, runAnalyzers: true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AnalyzerResults.Should().Contain(r => r.Contains("Result"));
    }
}
