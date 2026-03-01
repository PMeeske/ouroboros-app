using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Moq;
using Ouroboros.ApiHost.Client;
using Ouroboros.ApiHost.Models;

namespace Ouroboros.Tests.Client;

[Trait("Category", "Unit")]
public sealed class OuroborosApiClientTests
{
    [Fact]
    public void Constructor_NullFactory_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new OuroborosApiClient(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("factory");
    }

    [Fact]
    public async Task AskAsync_SuccessfulResponse_ReturnsAnswer()
    {
        // Arrange
        var apiResponse = new ApiResponse<AskResponse>
        {
            Success = true,
            Data = new AskResponse { Answer = "42" }
        };
        var json = JsonSerializer.Serialize(apiResponse, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var handler = new FakeHttpMessageHandler(json, HttpStatusCode.OK);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000/") };

        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(OuroborosApiClientConstants.HttpClientName))
            .Returns(httpClient);

        var client = new OuroborosApiClient(mockFactory.Object);

        // Act
        var result = await client.AskAsync("What is 6*7?");

        // Assert
        result.Should().Be("42");
    }

    [Fact]
    public async Task AskAsync_EmptyResponse_ThrowsInvalidOperationException()
    {
        // Arrange
        var apiResponse = new ApiResponse<AskResponse> { Success = true, Data = null };
        var json = JsonSerializer.Serialize(apiResponse, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var handler = new FakeHttpMessageHandler(json, HttpStatusCode.OK);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000/") };

        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(OuroborosApiClientConstants.HttpClientName))
            .Returns(httpClient);

        var client = new OuroborosApiClient(mockFactory.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.AskAsync("test"));
    }

    [Fact]
    public async Task ExecutePipelineAsync_SuccessfulResponse_ReturnsResult()
    {
        // Arrange
        var apiResponse = new ApiResponse<PipelineResponse>
        {
            Success = true,
            Data = new PipelineResponse { Result = "pipeline output" }
        };
        var json = JsonSerializer.Serialize(apiResponse, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var handler = new FakeHttpMessageHandler(json, HttpStatusCode.OK);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000/") };

        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(OuroborosApiClientConstants.HttpClientName))
            .Returns(httpClient);

        var client = new OuroborosApiClient(mockFactory.Object);

        // Act
        var result = await client.ExecutePipelineAsync("SetTopic('AI') | UseDraft");

        // Assert
        result.Should().Be("pipeline output");
    }

    [Fact]
    public async Task GetSelfStateAsync_SuccessfulResponse_ReturnsSelfState()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var apiResponse = new ApiResponse<SelfStateResponse>
        {
            Success = true,
            Data = new SelfStateResponse { AgentId = agentId, Name = "TestAgent" }
        };
        var json = JsonSerializer.Serialize(apiResponse, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var handler = new FakeHttpMessageHandler(json, HttpStatusCode.OK);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000/") };

        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(OuroborosApiClientConstants.HttpClientName))
            .Returns(httpClient);

        var client = new OuroborosApiClient(mockFactory.Object);

        // Act
        var result = await client.GetSelfStateAsync();

        // Assert
        result.Should().NotBeNull();
        result.AgentId.Should().Be(agentId);
        result.Name.Should().Be("TestAgent");
    }

    [Fact]
    public async Task GetSelfForecastAsync_EmptyResponse_ThrowsInvalidOperationException()
    {
        // Arrange
        var apiResponse = new ApiResponse<SelfForecastResponse> { Success = true, Data = null };
        var json = JsonSerializer.Serialize(apiResponse, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var handler = new FakeHttpMessageHandler(json, HttpStatusCode.OK);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000/") };

        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(OuroborosApiClientConstants.HttpClientName))
            .Returns(httpClient);

        var client = new OuroborosApiClient(mockFactory.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetSelfForecastAsync());
    }

    [Fact]
    public async Task GetCommitmentsAsync_SuccessfulResponse_ReturnsList()
    {
        // Arrange
        var commitments = new List<CommitmentDto>
        {
            new() { Id = Guid.NewGuid(), Description = "task1", Status = "Active" }
        };
        var apiResponse = new ApiResponse<List<CommitmentDto>>
        {
            Success = true,
            Data = commitments
        };
        var json = JsonSerializer.Serialize(apiResponse, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var handler = new FakeHttpMessageHandler(json, HttpStatusCode.OK);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000/") };

        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(OuroborosApiClientConstants.HttpClientName))
            .Returns(httpClient);

        var client = new OuroborosApiClient(mockFactory.Object);

        // Act
        var result = await client.GetCommitmentsAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].Description.Should().Be("task1");
    }

    /// <summary>
    /// Simple fake HTTP handler for unit testing.
    /// </summary>
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseContent;
        private readonly HttpStatusCode _statusCode;

        public FakeHttpMessageHandler(string responseContent, HttpStatusCode statusCode)
        {
            _responseContent = responseContent;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseContent, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
