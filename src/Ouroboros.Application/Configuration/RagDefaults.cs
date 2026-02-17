namespace Ouroboros.Application.Configuration;

/// <summary>
/// Default settings for RAG operations.
/// </summary>
public static class RagDefaults
{
    /// <summary>Default group size for divide-and-conquer RAG.</summary>
    public const int GroupSize = 6;
    
    /// <summary>Default number of sub-questions for decompose-and-aggregate RAG.</summary>
    public const int SubQuestions = 4;
    
    /// <summary>Default number of documents per sub-question.</summary>
    public const int DocumentsPerSubQuestion = 6;
}