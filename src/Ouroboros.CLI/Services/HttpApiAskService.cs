// <copyright file="HttpApiAskService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.ApiHost.Client;

namespace Ouroboros.CLI.Services;

/// <summary>
/// <see cref="IAskService"/> implementation that delegates to a remote Ouroboros
/// Web API instance.  Activated when the CLI is started with <c>--api-url</c>,
/// making the API a complete upstream provider instead of running the pipeline
/// locally.
/// </summary>
internal sealed class HttpApiAskService : IAskService
{
    private readonly IOuroborosApiClient _client;

    public HttpApiAskService(IOuroborosApiClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <inheritdoc/>
    public Task<string> AskAsync(AskRequest request, CancellationToken cancellationToken = default)
        => _client.AskAsync(request.Question, request.UseRag);

    /// <inheritdoc/>
    public Task<string> AskAsync(string question, bool useRag = false)
        => _client.AskAsync(question, useRag);
}
