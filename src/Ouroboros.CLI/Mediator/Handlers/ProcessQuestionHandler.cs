using MediatR;
using Ouroboros.CLI.Commands;

namespace Ouroboros.CLI.Mediator;

/// <summary>
/// MediatR handler for <see cref="ProcessQuestionRequest"/>.
/// Extracted from <c>OuroborosAgent.ProcessQuestionAsync</c>.
/// Runs a question through the chat pipeline, speaks the response, and records history.
/// </summary>
public sealed class ProcessQuestionHandler : IRequestHandler<ProcessQuestionRequest, string>
{
    private readonly OuroborosAgent _agent;
    private readonly IMediator _mediator;

    public ProcessQuestionHandler(OuroborosAgent agent, IMediator mediator)
    {
        _agent = agent;
        _mediator = mediator;
    }

    public async Task<string> Handle(ProcessQuestionRequest request, CancellationToken cancellationToken)
    {
        var question = request.Question;

        // ChatAsync is now dispatched through mediator
        var response = await _mediator.Send(new ChatRequest(question), cancellationToken);

        // SayWithVoiceAsync → _agent.VoiceService.SayAsync
        await _agent.VoiceService.SayAsync(response);

        // Say → side channel
        _agent.VoiceSub.SideChannel?.Say(response, _agent.Config.Persona);

        // Update conversation history
        _agent.MemorySub.ConversationHistory.Add($"User: {question}");
        _agent.MemorySub.ConversationHistory.Add($"Ouroboros: {response}");

        return response;
    }
}
