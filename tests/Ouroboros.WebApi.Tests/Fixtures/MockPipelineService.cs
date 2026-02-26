// <copyright file="MockPipelineService.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.ApiHost.Models;
using Ouroboros.ApiHost.Services;

namespace Ouroboros.Tests.WebApi.Fixtures;

/// <summary>
/// Mock implementation of IPipelineService for testing.
/// </summary>
public class MockPipelineService : IPipelineService
{
    private string _askResponse = "This is a mock answer to your question.";
    private string _pipelineResponse = "Mock pipeline execution result.";
    private Exception? _askException = null;
    private Exception? _pipelineException = null;

    /// <summary>
    /// Sets the response that will be returned by AskAsync.
    /// </summary>
    public void SetAskResponse(string response)
    {
        _askResponse = response;
    }

    /// <summary>
    /// Sets the response that will be returned by ExecutePipelineAsync.
    /// </summary>
    public void SetPipelineResponse(string response)
    {
        _pipelineResponse = response;
    }

    /// <summary>
    /// Sets an exception that will be thrown by AskAsync.
    /// </summary>
    public void SetAskException(Exception exception)
    {
        _askException = exception;
    }

    /// <summary>
    /// Sets an exception that will be thrown by ExecutePipelineAsync.
    /// </summary>
    public void SetPipelineException(Exception exception)
    {
        _pipelineException = exception;
    }

    /// <inheritdoc/>
    public Task<string> AskAsync(AskRequest request, CancellationToken cancellationToken = default)
    {
        if (_askException != null)
        {
            throw _askException;
        }

        if (request.UseRag)
        {
            return Task.FromResult($"[RAG] {_askResponse}");
        }

        return Task.FromResult(_askResponse);
    }

    /// <inheritdoc/>
    public Task<string> ExecutePipelineAsync(PipelineRequest request, CancellationToken cancellationToken = default)
    {
        if (_pipelineException != null)
        {
            throw _pipelineException;
        }

        if (request.Debug)
        {
            return Task.FromResult($"[DEBUG] {_pipelineResponse}");
        }

        return Task.FromResult(_pipelineResponse);
    }
}
