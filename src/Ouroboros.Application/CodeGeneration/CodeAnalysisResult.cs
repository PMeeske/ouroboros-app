using Microsoft.CodeAnalysis;

namespace Ouroboros.Application.CodeGeneration;

/// <summary>
/// Result of comprehensive code analysis including Roslyn analyzers.
/// </summary>
public class CodeAnalysisResult
{
    public bool IsValid { get; set; }
    public List<string> Diagnostics { get; set; } = new List<string>();
    public List<string> Classes { get; set; } = new List<string>();
    public List<string> Methods { get; set; } = new List<string>();
    public List<string> Usings { get; set; } = new List<string>();
    public List<string> AnalyzerResults { get; set; } = new List<string>();
    public SyntaxTree? SyntaxTree { get; set; }
    public Compilation? Compilation { get; set; }
}