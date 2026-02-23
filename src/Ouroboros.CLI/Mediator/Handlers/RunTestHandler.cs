using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="RunTestRequest"/>.
/// Extracted from <c>OuroborosAgent.RunTestAsync</c>.
/// </summary>
public sealed class RunTestHandler : IRequestHandler<RunTestRequest, string>
{
    private readonly OuroborosAgent _agent;
    private readonly IMediator _mediator;

    public RunTestHandler(OuroborosAgent agent, IMediator mediator)
    {
        _agent = agent;
        _mediator = mediator;
    }

    public async Task<string> Handle(RunTestRequest request, CancellationToken cancellationToken)
    {
        var testSpec = request.TestSpec;

        if (string.IsNullOrWhiteSpace(testSpec))
        {
            return @"Test Commands:
• 'test llm' - Test LLM connectivity
• 'test metta' - Test MeTTa engine
• 'test embedding' - Test embedding model
• 'test all' - Run all connectivity tests";
        }

        var cmd = testSpec.ToLowerInvariant().Trim();

        var chatModel = _agent.ModelsSub.ChatModel;
        var mettaEngine = _agent.MemorySub.MeTTaEngine;
        var embedding = _agent.ModelsSub.Embedding;
        var config = _agent.Config;

        if (cmd == "llm")
        {
            if (chatModel == null) return "✗ LLM: Not configured";
            try
            {
                var response = await chatModel.GenerateTextAsync("Say OK");
                return $"✓ LLM: {config.Model} responds correctly";
            }
            catch (Exception ex)
            {
                return $"✗ LLM: {ex.Message}";
            }
        }

        if (cmd == "metta")
        {
            if (mettaEngine == null) return "✗ MeTTa: Not configured";
            var result = await mettaEngine.ExecuteQueryAsync("!(+ 1 2)", CancellationToken.None);
            return result.Match(
                output => $"✓ MeTTa: Engine working (1+2={output})",
                error => $"✗ MeTTa: {error}");
        }

        if (cmd == "embedding")
        {
            if (embedding == null) return "✗ Embedding: Not configured";
            try
            {
                var vec = await embedding.CreateEmbeddingsAsync("test");
                return $"✓ Embedding: {config.EmbedModel} (dim={vec.Length})";
            }
            catch (Exception ex)
            {
                return $"✗ Embedding: {ex.Message}";
            }
        }

        if (cmd == "all")
        {
            var results = new List<string>
            {
                await _mediator.Send(new RunTestRequest("llm"), cancellationToken),
                await _mediator.Send(new RunTestRequest("metta"), cancellationToken),
                await _mediator.Send(new RunTestRequest("embedding"), cancellationToken)
            };
            return "Test Results:\n" + string.Join("\n", results);
        }

        return $"Unknown test: {testSpec}. Try 'test llm', 'test metta', 'test embedding', or 'test all'.";
    }
}
