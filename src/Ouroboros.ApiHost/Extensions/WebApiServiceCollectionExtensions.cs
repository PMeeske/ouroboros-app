// <copyright file="WebApiServiceCollectionExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Ouroboros.ApiHost.Client;

namespace Ouroboros.ApiHost.Extensions;

/// <summary>
/// <see cref="IServiceCollection"/> extensions that register all Ouroboros Web API
/// services. Call <c>AddOuroborosWebApi()</c> from any host – standalone
/// <c>WebApplication</c>, CLI with <c>--serve</c>, or an Android MAUI background
/// service – to get a fully functional AI pipeline server.
/// </summary>
public static class WebApiServiceCollectionExtensions
{
    /// <summary>
    /// Registers all services required to host the Ouroboros Web API inside any
    /// <see cref="IServiceCollection"/>-based host.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <param name="allowedOrigins">
    /// Optional CORS origins. <c>null</c> falls back to environment-aware defaults:
    /// any origin in local-dev mode, a placeholder origin otherwise.
    /// </param>
    /// <returns><paramref name="services"/> for fluent chaining.</returns>
    public static IServiceCollection AddOuroborosWebApi(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        string[]? allowedOrigins = null)
    {
        // ── Shared engine + foundational dependencies ────────────────────────
        // Cognitive physics, self-model, health checks — shared with CLI host.
        services.AddOuroborosEngine(configuration);

        // ── Web API–specific services ────────────────────────────────────────

        // Swagger / OpenAPI
        services.AddEndpointsApiExplorer();
        services.AddLocalization(options => options.ResourcesPath = "Resources");
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Ouroboros API",
                Version = "v1",
                Description = "Kubernetes-friendly ASP.NET Core Web API for Ouroboros – A functional programming-based AI pipeline system",
            });
            c.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.OpenApiSecurityScheme
            {
                Name = "X-Api-Key",
                Type = Microsoft.OpenApi.SecuritySchemeType.ApiKey,
                In = Microsoft.OpenApi.ParameterLocation.Header,
                Description = "API key for authentication. Set the 'ApiKey' configuration value to enable."
            });
        });

        // Pipeline service (Web API flavour — accepts AskRequest / PipelineRequest DTOs)
        services.AddSingleton<IPipelineService, PipelineService>();

        // Rate limiting — per-IP sliding window: 60 requests per minute
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 6,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 2,
                    }));
        });

        // CORS
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                if (allowedOrigins is { Length: > 0 })
                {
                    policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader();
                }
                else if (Ouroboros.Core.EnvironmentDetector.IsLocalDevelopment())
                {
                    policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                }
                else
                {
                    policy.WithOrigins("https://yourdomain.com")
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                }
            });
        });

        return services;
    }

    /// <summary>
    /// Registers an <see cref="HttpClient"/> preconfigured to call a remote Ouroboros
    /// Web API instance. Useful for Android or CLI clients that delegate to an
    /// upstream API server rather than running the pipeline locally.
    /// </summary>
    /// <param name="services">The DI container.</param>
    /// <param name="baseAddress">Base URL of the running Ouroboros API, e.g. <c>http://localhost:5000</c>.</param>
    /// <returns><paramref name="services"/> for fluent chaining.</returns>
    public static IServiceCollection AddOuroborosApiClient(
        this IServiceCollection services,
        string baseAddress)
    {
        services.AddHttpClient(OuroborosApiClientConstants.HttpClientName, client =>
        {
            client.BaseAddress = new Uri(baseAddress.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        services.AddSingleton<IOuroborosApiClient>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            return new OuroborosApiClient(factory);
        });

        return services;
    }
}
