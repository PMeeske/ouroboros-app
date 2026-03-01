using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Ouroboros.Application.CodeGeneration;

/// <summary>
/// Partial class containing private helper methods: analyzers, parsers, and utilities.
/// </summary>
public partial class RoslynCodeTool
{
    private async Task<List<string>> RunCustomAnalyzersAsync(SyntaxTree tree, Compilation compilation)
    {
        List<string> results = new List<string>();

        // Monadic pattern analyzer
        results.AddRange(await AnalyzeMonadicPatternsAsync(tree));

        // Async/await analyzer
        results.AddRange(AnalyzeAsyncPatterns(tree));

        // Documentation analyzer
        results.AddRange(AnalyzeDocumentation(tree));

        return results;
    }

    private async Task<List<string>> AnalyzeMonadicPatternsAsync(SyntaxTree tree)
    {
        List<string> findings = new List<string>();
        CompilationUnitSyntax root = await tree.GetRootAsync() as CompilationUnitSyntax ?? throw new InvalidOperationException();

        // Check for Result<T> usage
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        foreach (var method in methods)
        {
            if (method.ReturnType.ToString().Contains("Task") &&
                !method.ReturnType.ToString().Contains("Result"))
            {
                findings.Add($"Method {method.Identifier} returns Task but not Result<T>. Consider using Result monad.");
            }
        }

        return findings;
    }

    private List<string> AnalyzeAsyncPatterns(SyntaxTree tree)
    {
        List<string> findings = new List<string>();
        CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

        // Check for blocking async calls
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var invocation in invocations)
        {
            string invocationText = invocation.ToString();
            if (invocationText.Contains(".Result") || invocationText.Contains(".Wait()"))
            {
                findings.Add($"Potential blocking async call at line {invocation.GetLocation().GetLineSpan().StartLinePosition.Line}: {invocationText}");
            }
        }

        return findings;
    }

    private List<string> AnalyzeDocumentation(SyntaxTree tree)
    {
        List<string> findings = new List<string>();
        CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

        // Check for missing XML documentation on public members
        var publicMembers = root.DescendantNodes()
            .Where(n => n is ClassDeclarationSyntax || n is MethodDeclarationSyntax)
            .Where(n =>
            {
                SyntaxTokenList modifiers = n switch
                {
                    ClassDeclarationSyntax c => c.Modifiers,
                    MethodDeclarationSyntax m => m.Modifiers,
                    _ => default
                };
                return modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
            });

        foreach (var member in publicMembers)
        {
            bool hasDocComment = member.GetLeadingTrivia()
                .Any(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                          t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));

            if (!hasDocComment)
            {
                string memberName = member switch
                {
                    ClassDeclarationSyntax c => c.Identifier.Text,
                    MethodDeclarationSyntax m => m.Identifier.Text,
                    _ => "unknown"
                };
                findings.Add($"Public member '{memberName}' missing XML documentation");
            }
        }

        return findings;
    }

    private PropertyDeclarationSyntax ParseProperty(string propertySpec)
    {
        // Parse various formats: "public string Name { get; set; }" or "string Name" or "Type Name"
        string trimmed = propertySpec.Trim().TrimEnd(';');

        // Try to parse as full property declaration
        MemberDeclarationSyntax? member = SyntaxFactory.ParseMemberDeclaration(trimmed);
        if (member is PropertyDeclarationSyntax existingProp)
        {
            return existingProp;
        }

        // Parse simple format: "Type Name"
        string[] parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            throw new ArgumentException($"Invalid property specification: {propertySpec}");
        }

        string typeName = parts[^2];
        string propName = parts[^1];

        return SyntaxFactory.PropertyDeclaration(
            SyntaxFactory.ParseTypeName(typeName),
            propName)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .AddAccessorListAccessors(
                SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
    }

    private MethodDeclarationSyntax ParseMethod(string methodSpec, string? body = null)
    {
        // Parse: "public async Task<Result<string>> DoSomething(string arg)" or "DoSomething"
        string methodCode = body != null
            ? $"{methodSpec} {{ {body} }}"
            : methodSpec.Contains("(") ? $"{methodSpec} {{ }}" : $"public void {methodSpec}() {{ }}";

        MemberDeclarationSyntax? member = SyntaxFactory.ParseMemberDeclaration(methodCode);
        if (member is MethodDeclarationSyntax method)
        {
            return method;
        }

        // Fallback: create simple method
        return SyntaxFactory.MethodDeclaration(
            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
            methodSpec)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .WithBody(SyntaxFactory.Block());
    }

    private T AddXmlDocComment<T>(T member, string summary) where T : MemberDeclarationSyntax
    {
        string xmlDoc = $@"/// <summary>
/// {summary}
/// </summary>";

        SyntaxTriviaList trivia = SyntaxFactory.ParseLeadingTrivia(xmlDoc + Environment.NewLine);
        return member.WithLeadingTrivia(trivia);
    }

    private TextSpan GetSpanForLines(SourceText text, int startLine, int endLine)
    {
        TextLine start = text.Lines[startLine];
        TextLine end = text.Lines[endLine];
        return TextSpan.FromBounds(start.Start, end.End);
    }

    private string ExtractCodeFromMarkdown(string response)
    {
        // Extract code from markdown code blocks
        System.Text.RegularExpressions.Match match = MarkdownCodeBlockRegex().Match(response);

        return match.Success ? match.Groups[1].Value.Trim() : response.Trim();
    }

    private IEnumerable<MetadataReference> GetDefaultReferences()
    {
        // Add common .NET references for compilation
        string assemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location) ?? string.Empty;

        return new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Collections.dll")),
            MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Linq.dll")),
        };
    }
}
