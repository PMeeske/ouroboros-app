using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using Ouroboros.Roslynator.Providers;

namespace Ouroboros.Application.CodeGeneration;

/// <summary>
/// Roslyn-based code analysis, generation, and refactoring tool.
/// Provides full code creation/edit capabilities with analyzer support.
/// </summary>
public partial class RoslynCodeTool
{
    private readonly AdhocWorkspace _workspace;

    public RoslynCodeTool()
    {
        _workspace = new AdhocWorkspace();
    }

    /// <summary>
    /// Analyzes C# code and returns diagnostics, syntax tree information, and analyzer results.
    /// </summary>
    public async Task<Result<CodeAnalysisResult, string>> AnalyzeCodeAsync(
        string code,
        string? filePath = null,
        bool runAnalyzers = true)
    {
        try
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(code, path: filePath ?? "code.cs");
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

            // Create compilation for full analysis
            CSharpCompilation compilation = CSharpCompilation.Create(
                "Analysis",
                new[] { tree },
                GetDefaultReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            // Get diagnostics
            IEnumerable<Diagnostic> allDiagnostics = compilation.GetDiagnostics();
            List<string> diagnostics = allDiagnostics
                .Select(d => $"{d.Severity}: {d.Id} - {d.GetMessage()} at {d.Location.GetLineSpan()}")
                .ToList();

            // Extract code structure
            List<string> classes = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Select(c => c.Identifier.Text)
                .ToList();

            List<string> methods = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Select(m => $"{m.ReturnType} {m.Identifier.Text}({string.Join(", ", m.ParameterList.Parameters)})")
                .ToList();

            List<string> usings = root.Usings
                .Select(u => u.Name?.ToString() ?? string.Empty)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();

            // Run custom analyzers if requested
            List<string> analyzerResults = new List<string>();
            if (runAnalyzers)
            {
                analyzerResults = await RunCustomAnalyzersAsync(tree, compilation);
            }

            CodeAnalysisResult result = new CodeAnalysisResult
            {
                IsValid = !allDiagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                Diagnostics = diagnostics,
                Classes = classes,
                Methods = methods,
                Usings = usings,
                AnalyzerResults = analyzerResults,
                SyntaxTree = tree,
                Compilation = compilation
            };

            return Result<CodeAnalysisResult, string>.Success(result);
        }
        catch (ArgumentException ex)
        {
            return Result<CodeAnalysisResult, string>.Failure($"Code analysis failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a new C# class with specified structure using Roslyn generators.
    /// </summary>
    public Result<string, string> CreateClass(
        string className,
        string namespaceName,
        List<string>? methods = null,
        List<string>? properties = null,
        List<string>? usings = null,
        string? baseClass = null,
        List<string>? interfaces = null,
        bool addDocComments = true)
    {
        try
        {
            // Build using directives
            List<UsingDirectiveSyntax> usingDirectives = new List<UsingDirectiveSyntax>();
            foreach (string usingName in usings ?? new List<string> { "System", "System.Threading.Tasks" })
            {
                usingDirectives.Add(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(usingName)));
            }

            // Build base type list
            BaseListSyntax? baseList = null;
            if (!string.IsNullOrEmpty(baseClass) || (interfaces?.Count ?? 0) > 0)
            {
                List<BaseTypeSyntax> baseTypes = new List<BaseTypeSyntax>();
                if (!string.IsNullOrEmpty(baseClass))
                {
                    baseTypes.Add(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(baseClass)));
                }
                foreach (string iface in interfaces ?? new List<string>())
                {
                    baseTypes.Add(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(iface)));
                }
                baseList = SyntaxFactory.BaseList(SyntaxFactory.SeparatedList(baseTypes));
            }

            // Create class members
            List<MemberDeclarationSyntax> members = new List<MemberDeclarationSyntax>();

            // Add properties
            foreach (string prop in properties ?? new List<string>())
            {
                PropertyDeclarationSyntax property = ParseProperty(prop);
                if (addDocComments)
                {
                    property = AddXmlDocComment(property, $"Gets or sets the {property.Identifier.Text}.");
                }
                members.Add(property);
            }

            // Add methods
            foreach (string method in methods ?? new List<string>())
            {
                MethodDeclarationSyntax methodSyntax = ParseMethod(method);
                if (addDocComments)
                {
                    methodSyntax = AddXmlDocComment(methodSyntax, $"Executes {methodSyntax.Identifier.Text} operation.");
                }
                members.Add(methodSyntax);
            }

            // Build class declaration
            ClassDeclarationSyntax classDeclaration = SyntaxFactory.ClassDeclaration(className)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .WithMembers(SyntaxFactory.List(members));

            if (baseList != null)
            {
                classDeclaration = classDeclaration.WithBaseList(baseList);
            }

            if (addDocComments)
            {
                classDeclaration = AddXmlDocComment(classDeclaration, $"Represents a {className}.");
            }

            // Build namespace
            NamespaceDeclarationSyntax namespaceDeclaration = SyntaxFactory.NamespaceDeclaration(
                SyntaxFactory.ParseName(namespaceName))
                .AddMembers(classDeclaration);

            // Build compilation unit
            CompilationUnitSyntax compilationUnit = SyntaxFactory.CompilationUnit()
                .AddUsings(usingDirectives.ToArray())
                .AddMembers(namespaceDeclaration)
                .WithLeadingTrivia(
                    SyntaxFactory.Comment("#pragma warning disable CS1591"));

            // Format code
            SyntaxNode formatted = Formatter.Format(compilationUnit, _workspace);
            string code = formatted.ToFullString();

            return Result<string, string>.Success(code);
        }
        catch (ArgumentException ex)
        {
            return Result<string, string>.Failure($"Class creation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds a method to an existing class.
    /// </summary>
    public Result<string, string> AddMethodToClass(
        string code,
        string className,
        string methodSignature,
        string? methodBody = null)
    {
        try
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(code);
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

            // Find the class
            ClassDeclarationSyntax? targetClass = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.Text == className);

            if (targetClass == null)
            {
                return Result<string, string>.Failure($"Class '{className}' not found");
            }

            // Parse method
            MethodDeclarationSyntax newMethod = ParseMethod(methodSignature, methodBody);

            // Add method to class
            ClassDeclarationSyntax modifiedClass = targetClass.AddMembers(newMethod);

            // Replace in tree
            SyntaxNode newRoot = root.ReplaceNode(targetClass, modifiedClass);
            SyntaxNode formatted = Formatter.Format(newRoot, _workspace);

            return Result<string, string>.Success(formatted.ToFullString());
        }
        catch (ArgumentException ex)
        {
            return Result<string, string>.Failure($"Failed to add method: {ex.Message}");
        }
    }

    /// <summary>
    /// Refactors code by renaming a symbol (Roslyn-powered rename refactoring).
    /// </summary>
    public Result<string, string> RenameSymbol(string code, string oldName, string newName)
    {
        try
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(code);
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

            // First pass: handle declarations (variable declarations, parameters, etc.)
            // Must get fresh tokens from the current root at each step
            SyntaxNode currentRoot = root;
            
            // Find and replace declaration tokens first
            while (true)
            {
                SyntaxToken? tokenToReplace = currentRoot.DescendantTokens()
                    .FirstOrDefault(t => t.IsKind(SyntaxKind.IdentifierToken) && t.Text == oldName);
                
                if (tokenToReplace == null || tokenToReplace.Value == default)
                    break;
                    
                currentRoot = currentRoot.ReplaceToken(
                    tokenToReplace.Value, 
                    SyntaxFactory.Identifier(newName).WithTriviaFrom(tokenToReplace.Value));
            }

            SyntaxNode formatted = Formatter.Format(currentRoot, _workspace);
            return Result<string, string>.Success(formatted.ToFullString());
        }
        catch (ArgumentException ex)
        {
            return Result<string, string>.Failure($"Rename failed: {ex.Message}");
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"```(?:csharp|cs|c#)?\s*\n(.*?)\n```", System.Text.RegularExpressions.RegexOptions.Singleline)]
    private static partial System.Text.RegularExpressions.Regex MarkdownCodeBlockRegex();
}