using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using Ouroboros.Roslynator.Providers;

namespace Ouroboros.Application.CodeGeneration;

/// <summary>
/// Partial class containing refactoring, code generation, and fix operations.
/// </summary>
public partial class RoslynCodeTool
{
    /// <summary>
    /// Extracts a method from selected code (Roslyn refactoring).
    /// </summary>
    public Result<string, string> ExtractMethod(
        string code,
        int startLine,
        int endLine,
        string newMethodName)
    {
        try
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(code);
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

            // Find statements in the range
            TextSpan span = GetSpanForLines(tree.GetText(), startLine, endLine);
            IEnumerable<StatementSyntax> statements = root.DescendantNodes()
                .OfType<StatementSyntax>()
                .Where(s => span.Contains(s.Span));

            if (!statements.Any())
            {
                return Result<string, string>.Failure("No statements found in the specified range");
            }

            // Create new method
            MethodDeclarationSyntax newMethod = SyntaxFactory.MethodDeclaration(
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                newMethodName)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword))
                .WithBody(SyntaxFactory.Block(statements));

            // Find containing method
            MethodDeclarationSyntax? containingMethod = statements.First()
                .Ancestors()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault();

            if (containingMethod == null)
            {
                return Result<string, string>.Failure("Could not find containing method");
            }

            // Replace statements with method call
            ExpressionStatementSyntax methodCall = SyntaxFactory.ExpressionStatement(
                SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(newMethodName)));

            BlockSyntax? originalBody = containingMethod.Body;
            if (originalBody == null)
            {
                return Result<string, string>.Failure("Method has no body");
            }

            // Remove extracted statements and add method call
            List<StatementSyntax> newStatements = originalBody.Statements
                .Where(s => !statements.Contains(s))
                .ToList();

            // Insert method call at the position of first extracted statement
            int insertIndex = originalBody.Statements.IndexOf(statements.First());
            newStatements.Insert(insertIndex, methodCall);

            BlockSyntax newBody = SyntaxFactory.Block(newStatements);
            MethodDeclarationSyntax modifiedMethod = containingMethod.WithBody(newBody);

            // Find containing class and add new method
            ClassDeclarationSyntax? containingClass = containingMethod
                .Ancestors()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault();

            if (containingClass == null)
            {
                return Result<string, string>.Failure("Could not find containing class");
            }

            ClassDeclarationSyntax modifiedClass = containingClass
                .ReplaceNode(containingMethod, modifiedMethod)
                .AddMembers(newMethod);

            SyntaxNode newRoot = root.ReplaceNode(containingClass, modifiedClass);
            SyntaxNode formatted = Formatter.Format(newRoot, _workspace);

            return Result<string, string>.Success(formatted.ToFullString());
        }
        catch (ArgumentException ex)
        {
            return Result<string, string>.Failure($"Extract method failed: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            return Result<string, string>.Failure($"Extract method failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates code based on natural language description using AI + Roslyn validation.
    /// </summary>
    public async Task<Result<string, string>> GenerateCodeFromDescriptionAsync(
        string description,
        string codeContext,
        ToolAwareChatModel llm)
    {
        try
        {
            string prompt = $@"You are an expert C# developer using Roslyn and functional programming patterns.

Context:
{codeContext}

Task: {description}

Generate complete, production-quality C# code that:
1. Follows functional programming principles (immutability, pure functions)
2. Uses Result<T> and Option<T> monads for error handling
3. Includes XML documentation comments (///)
4. Is properly formatted
5. Includes appropriate using statements
6. Uses async/await for I/O operations
7. Follows Ouroboros conventions

Respond with only the C# code, no explanations.";

            (string response, List<ToolExecution> _) = await llm.GenerateWithToolsAsync(prompt);

            // Extract code block if wrapped in markdown
            string code = ExtractCodeFromMarkdown(response);

            // Validate generated code with Roslyn
            Result<CodeAnalysisResult, string> analysisResult = await AnalyzeCodeAsync(code);
            if (analysisResult.IsFailure)
            {
                return Result<string, string>.Failure($"Generated code analysis failed: {analysisResult.Error}");
            }

            if (!analysisResult.Value.IsValid)
            {
                string errors = string.Join("\n", analysisResult.Value.Diagnostics.Take(5));
                return Result<string, string>.Failure($"Generated code has errors:\n{errors}");
            }

            return Result<string, string>.Success(code);
        }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"Code generation failed: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            return Result<string, string>.Failure($"Code generation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates a source generator for compile-time code generation.
    /// </summary>
    public Result<string, string> GenerateSourceGenerator(string generatorName, string generationLogic)
    {
        try
        {
            string sourceGeneratorCode = $@"#pragma warning disable CS1591
using System;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Ouroboros.SourceGenerators
{{
    [Generator]
    public class {generatorName} : ISourceGenerator
    {{
        public void Initialize(GeneratorInitializationContext context)
        {{
            // Register syntax receiver if needed
        }}

        public void Execute(GeneratorExecutionContext context)
        {{
            try
            {{
                {generationLogic}

                // Example: Add generated source
                context.AddSource(""{generatorName}Generated.cs"", SourceText.From(generatedCode, Encoding.UTF8));
            }}
            catch (InvalidOperationException ex)
            {{
                // Report diagnostic
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        ""SG001"",
                        ""Source Generation Error"",
                        $""{{ex.Message}}"",
                        ""SourceGenerator"",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    Location.None));
            }}
        }}
    }}
}}";

            return Result<string, string>.Success(sourceGeneratorCode);
        }
        catch (ArgumentException ex)
        {
            return Result<string, string>.Failure($"Generator creation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Applies the UniversalCodeFixProvider (AI + Standard) to the given code.
    /// </summary>
    public async Task<Result<string, string>> ApplyUniversalFixAsync(string code, string diagnosticId)
    {
        try
        {
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = await tree.GetRootAsync();

            // Create a dummy compilation to get diagnostics
            var compilation = CSharpCompilation.Create("FixCompilation")
                .AddReferences(GetDefaultReferences())
                .AddSyntaxTrees(tree)
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithNullableContextOptions(NullableContextOptions.Enable));

            var diagnostics = compilation.GetDiagnostics();
            var targetDiagnostic = diagnostics.FirstOrDefault(d => d.Id == diagnosticId);

            if (targetDiagnostic == null)
            {
                var allDiags = string.Join(", ", diagnostics.Select(d => d.Id));
                return Result<string, string>.Failure($"Diagnostic {diagnosticId} not found in code. Found: {allDiags}");
            }

            // Create a dummy document/project/solution for the fix provider
            var workspace = new AdhocWorkspace();
            var project = workspace.AddProject("FixProject", LanguageNames.CSharp)
                .AddMetadataReferences(GetDefaultReferences());
            var document = project.AddDocument("FixDocument.cs", root);

            // Re-fetch diagnostic on the document context if needed, or just pass the one we have
            // Note: FixChain.ExecuteAsync takes a Document and Diagnostic.

            // Instantiate the provider to access the chain logic directly or via its public methods if exposed.
            // However, UniversalCodeFixProvider is a CodeFixProvider.
            // We can access the inner chain logic if we made it public or use the provider's logic.
            // The UniversalCodeFixProvider.ConcreteChains.UniversalChain is private/internal.
            // But we can instantiate UniversalCodeFixProvider and see if we can leverage it,
            // OR we can just use the FixChain infrastructure if we expose the specific chain.

            // Let's assume we want to run the specific UniversalChain logic.
            // Since UniversalCodeFixProvider defines the chain in RegisterCodeFixesAsync,
            // we might need to expose the chain or duplicate the composition here.
            // Let's look at UniversalCodeFixProvider again. It has a nested class ConcreteChains.UniversalChain.

            var chain = new UniversalCodeFixProvider.ConcreteChains.UniversalChain();
            Document newDocument = await chain.ExecuteAsync(document, targetDiagnostic, CancellationToken.None);

            SyntaxNode? newRoot = await newDocument.GetSyntaxRootAsync();
            return Result<string, string>.Success(newRoot?.ToFullString() ?? code);
        }
        catch (ArgumentException ex)
        {
            return Result<string, string>.Failure($"Fix failed: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            return Result<string, string>.Failure($"Fix failed: {ex.Message}");
        }
    }
}
