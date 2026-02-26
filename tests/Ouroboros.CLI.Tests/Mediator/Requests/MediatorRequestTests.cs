using MediatR;
using Ouroboros.CLI.Mediator;
using Ouroboros.CLI.Services;

namespace Ouroboros.Tests.CLI.Mediator.Requests;

[Trait("Category", "Unit")]
public class MediatorRequestTests
{
    [Fact]
    public void ChatRequest_SetsInput()
    {
        var request = new ChatRequest("Hello");
        request.Input.Should().Be("Hello");
    }

    [Fact]
    public void ChatRequest_ImplementsIRequest()
    {
        var request = new ChatRequest("Hello");
        request.Should().BeAssignableTo<IRequest<string>>();
    }

    [Fact]
    public void RememberRequest_SetsInfo()
    {
        var request = new RememberRequest("important fact");
        request.Info.Should().Be("important fact");
    }

    [Fact]
    public void RecallRequest_SetsTopic()
    {
        var request = new RecallRequest("architecture");
        request.Topic.Should().Be("architecture");
    }

    [Fact]
    public void RunTestRequest_SetsTestSpec()
    {
        var request = new RunTestRequest("llm");
        request.TestSpec.Should().Be("llm");
    }

    [Fact]
    public void AskQuery_SetsRequest()
    {
        var askReq = new AskRequest("What is 2+2?");
        var query = new AskQuery(askReq);

        query.Request.Should().BeSameAs(askReq);
        query.Request.Question.Should().Be("What is 2+2?");
    }

    [Fact]
    public void ChatRequest_Equality()
    {
        var r1 = new ChatRequest("Hello");
        var r2 = new ChatRequest("Hello");
        r1.Should().Be(r2);
    }

    [Fact]
    public void RememberRequest_Equality()
    {
        var r1 = new RememberRequest("fact");
        var r2 = new RememberRequest("fact");
        r1.Should().Be(r2);
    }
}
