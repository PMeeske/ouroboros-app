// <copyright file="HttpApiPipelineService.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.ApiHost.Client;

namespace Ouroboros.CLI.Services;

/// <summary>
/// <see cref="IPipelineService"/> implementation that delegates DSL pipeline
/// execution to a remote Ouroboros Web API instance.  Activated when the CLI
/// is started with <c>--api-url</c>, making the API a complete upstream provider.
/// </summary>
internal sealed class HttpApiPipelineService : IPipelineService
{
    private readonly IOuroborosApiClient _client;

    public HttpApiPipelineService(IOuroborosApiClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <inheritdoc/>
    public Task<string> ExecutePipelineAsync(string dsl)
        => _client.ExecutePipelineAsync(dsl);
}
