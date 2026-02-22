using System.CommandLine;

namespace Ouroboros.CLI.Commands.Options;

/// <summary>
/// Options for the ask command. Composes shared option groups and adds ask-specific options.
/// </summary>
public sealed class AskCommandOptions
{
    // ── Composed option groups ─────────────────────────────────────────
    public ModelOptions Model { get; } = new();
    public EndpointOptions Endpoint { get; } = new();
    public MultiModelOptions MultiModel { get; } = new();
    public DiagnosticOptions Diagnostics { get; } = new();
    public EmbeddingOptions Embedding { get; } = new();
    public CollectiveOptions Collective { get; } = new();
    public AgentLoopOptions AgentLoop { get; } = new();
    public CommandVoiceOptions Voice { get; } = new();

    // ── Ask-specific options ───────────────────────────────────────────

    public Option<string> QuestionOption { get; } = new("--question", "-q")
    {
        Description = "The question to ask",
        DefaultValueFactory = _ => string.Empty
    };

    public Option<bool> RagOption { get; } = new("--rag")
    {
        Description = "Enable RAG context",
        DefaultValueFactory = _ => false
    };

    public Option<string?> CultureOption { get; } = new("--culture")
    {
        Description = "Target culture for the response (e.g. en-US, fr-FR, es)"
    };

    public Option<int> TopKOption { get; } = new("--topk")
    {
        Description = "Number of context documents (RAG mode)",
        DefaultValueFactory = _ => 3
    };

    public void AddToCommand(Command command)
    {
        // Ask-specific
        command.Add(QuestionOption);
        command.Add(RagOption);
        command.Add(CultureOption);
        command.Add(TopKOption);

        // Composed groups
        Model.AddToCommand(command);
        Endpoint.AddToCommand(command);
        MultiModel.AddToCommand(command);
        Diagnostics.AddToCommand(command);
        Embedding.AddToCommand(command);
        Collective.AddToCommand(command);
        AgentLoop.AddToCommand(command);
        Voice.AddToCommand(command);
    }
}
