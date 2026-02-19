// <copyright file="WebApiApplicationExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Ouroboros.ApiHost.Middleware;

namespace Ouroboros.ApiHost.Extensions;

/// <summary>
/// <see cref="WebApplication"/> extensions that configure the ASP.NET Core middleware
/// pipeline and map all Ouroboros API endpoints. After calling
/// <see cref="WebApiServiceCollectionExtensions.AddOuroborosWebApi"/> during host
/// construction, call these methods on the built <see cref="WebApplication"/> before
/// <c>app.Run()</c>.
/// </summary>
public static class WebApiApplicationExtensions
{
    /// <summary>
    /// Applies the Ouroboros middleware pipeline: request-localisation, correlation ID
    /// header, global exception handler, Swagger UI, and CORS.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns><paramref name="app"/> for fluent chaining.</returns>
    public static WebApplication UseOuroborosWebApi(this WebApplication app)
    {
        var supportedCultures = new[] { "en-US", "es", "fr", "de", "zh", "ja" };
        var localizationOptions = new RequestLocalizationOptions()
            .SetDefaultCulture(supportedCultures[0])
            .AddSupportedCultures(supportedCultures)
            .AddSupportedUICultures(supportedCultures);

        app.UseRequestLocalization(localizationOptions);

        // Middleware pipeline — order matters
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<GlobalExceptionMiddleware>();

        // Swagger (all environments — useful when embedded in CLI or Android dev build)
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Ouroboros API v1");
            c.RoutePrefix = "swagger";
        });

        app.UseCors();

        return app;
    }

    /// <summary>
    /// Maps all Ouroboros API endpoints: health probes, root info, /api/ask,
    /// /api/pipeline, and the Phase-2 /api/self/* self-model endpoints.
    /// </summary>
    /// <param name="app">The endpoint route builder (typically a <see cref="WebApplication"/>).</param>
    /// <returns><paramref name="app"/> for fluent chaining.</returns>
    public static WebApplication MapOuroborosApiEndpoints(this WebApplication app)
    {
        // Kubernetes health / readiness probes
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/ready");

        // Root discovery endpoint
        app.MapGet("/", () => Results.Ok(new
        {
            service = "Ouroboros Web API",
            version = "1.0.0",
            status = "running",
            documentation = "/swagger",
            environment = new
            {
                name = Ouroboros.Core.EnvironmentDetector.GetEnvironmentName() ?? "Unknown",
                isLocalDevelopment = Ouroboros.Core.EnvironmentDetector.IsLocalDevelopment(),
                isProduction = Ouroboros.Core.EnvironmentDetector.IsProduction(),
                isStaging = Ouroboros.Core.EnvironmentDetector.IsStaging(),
                isKubernetes = Ouroboros.Core.EnvironmentDetector.IsRunningInKubernetes(),
            },
            endpoints = new object[]
            {
                new { method = "GET",  path = "/health",               description = "Liveness probe for Kubernetes" },
                new { method = "GET",  path = "/ready",                description = "Readiness probe for Kubernetes" },
                new { method = "POST", path = "/api/ask",              description = "Ask a question (supports RAG and agent mode)" },
                new { method = "POST", path = "/api/pipeline",         description = "Execute a DSL pipeline" },
                new { method = "GET",  path = "/api/self/state",       description = "Get agent identity state" },
                new { method = "GET",  path = "/api/self/forecast",    description = "Get predictions and anomalies" },
                new { method = "GET",  path = "/api/self/commitments", description = "Get active commitments" },
                new { method = "POST", path = "/api/self/explain",     description = "Generate self-explanation from execution DAG" },
                new { method = "GET",  path = "/swagger",              description = "Interactive API documentation (Swagger UI)" },
            },
            quickStart = new
            {
                askExample = new { method = "POST", url = "/api/ask", body = new { question = "What is functional programming?", model = "llama3" } },
                pipelineExample = new { method = "POST", url = "/api/pipeline", body = new { dsl = "SetTopic('AI') | UseDraft | UseCritique" } },
            },
        }))
        .WithName("Root")
        .WithTags("System")
        .Produces(200);

        // ── AI Pipeline endpoints ─────────────────────────────────────────────────

        app.MapPost("/api/ask", async (AskRequest request, IPipelineService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Question))
                return Results.BadRequest(ApiResponse<AskResponse>.Fail("Question is required"));

            try
            {
                var sw = Stopwatch.StartNew();
                string answer = await service.AskAsync(request, ct);
                sw.Stop();

                return Results.Ok(ApiResponse<AskResponse>.Ok(
                    new AskResponse { Answer = answer, Model = request.Model ?? "llama3" },
                    sw.ElapsedMilliseconds));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(ApiResponse<AskResponse>.Fail($"Error: {ex.Message}"));
            }
        })
        .WithName("Ask")
        .WithTags("AI Pipeline")
        .WithOpenApi(op =>
        {
            op.Summary = "Ask a question to the AI pipeline";
            op.Description = "Supports both simple question-answering and RAG (Retrieval Augmented Generation) mode.";
            return op;
        })
        .Produces<ApiResponse<AskResponse>>(200)
        .Produces<ApiResponse<AskResponse>>(400);

        app.MapPost("/api/pipeline", async (PipelineRequest request, IPipelineService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Dsl))
                return Results.BadRequest(ApiResponse<PipelineResponse>.Fail("DSL is required"));

            try
            {
                var sw = Stopwatch.StartNew();
                string result = await service.ExecutePipelineAsync(request, ct);
                sw.Stop();

                return Results.Ok(ApiResponse<PipelineResponse>.Ok(
                    new PipelineResponse { Result = result, FinalState = "Completed" },
                    sw.ElapsedMilliseconds));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(ApiResponse<PipelineResponse>.Fail($"Error: {ex.Message}"));
            }
        })
        .WithName("ExecutePipeline")
        .WithTags("AI Pipeline")
        .WithOpenApi(op =>
        {
            op.Summary = "Execute a pipeline using DSL";
            op.Description = "Execute a pipeline using DSL syntax (e.g., 'SetTopic(\"AI\") | UseDraft | UseCritique | UseImprove')";
            return op;
        })
        .Produces<ApiResponse<PipelineResponse>>(200)
        .Produces<ApiResponse<PipelineResponse>>(400);

        // ── Phase 2 Self-Model endpoints ──────────────────────────────────────────

        app.MapGet("/api/self/state", async (ISelfModelService service, CancellationToken ct) =>
        {
            try
            {
                var sw = Stopwatch.StartNew();
                SelfStateResponse state = await service.GetStateAsync(ct);
                sw.Stop();
                return Results.Ok(ApiResponse<SelfStateResponse>.Ok(state, sw.ElapsedMilliseconds));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(ApiResponse<SelfStateResponse>.Fail($"Error: {ex.Message}"));
            }
        })
        .WithName("GetSelfState")
        .WithTags("Self-Model")
        .WithOpenApi(op =>
        {
            op.Summary = "Get current agent identity state";
            op.Description = "Returns the agent's complete identity state including capabilities, resources, commitments, and performance metrics.";
            return op;
        })
        .Produces<ApiResponse<SelfStateResponse>>(200)
        .Produces<ApiResponse<SelfStateResponse>>(400);

        app.MapGet("/api/self/forecast", async (ISelfModelService service, CancellationToken ct) =>
        {
            try
            {
                var sw = Stopwatch.StartNew();
                SelfForecastResponse forecasts = await service.GetForecastsAsync(ct);
                sw.Stop();
                return Results.Ok(ApiResponse<SelfForecastResponse>.Ok(forecasts, sw.ElapsedMilliseconds));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(ApiResponse<SelfForecastResponse>.Fail($"Error: {ex.Message}"));
            }
        })
        .WithName("GetSelfForecasts")
        .WithTags("Self-Model")
        .WithOpenApi(op =>
        {
            op.Summary = "Get agent forecasts and predictions";
            op.Description = "Returns pending forecasts, calibration metrics, and recent anomalies detected by the predictive monitor.";
            return op;
        })
        .Produces<ApiResponse<SelfForecastResponse>>(200)
        .Produces<ApiResponse<SelfForecastResponse>>(400);

        app.MapGet("/api/self/commitments", async (ISelfModelService service, CancellationToken ct) =>
        {
            try
            {
                var sw = Stopwatch.StartNew();
                List<CommitmentDto> commitments = await service.GetCommitmentsAsync(ct);
                sw.Stop();
                return Results.Ok(ApiResponse<List<CommitmentDto>>.Ok(commitments, sw.ElapsedMilliseconds));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(ApiResponse<List<CommitmentDto>>.Fail($"Error: {ex.Message}"));
            }
        })
        .WithName("GetSelfCommitments")
        .WithTags("Self-Model")
        .WithOpenApi(op =>
        {
            op.Summary = "Get active agent commitments";
            op.Description = "Returns all active commitments with their status, progress, and priority.";
            return op;
        })
        .Produces<ApiResponse<List<CommitmentDto>>>(200)
        .Produces<ApiResponse<List<CommitmentDto>>>(400);

        app.MapPost("/api/self/explain", async (SelfExplainRequest request, ISelfModelService service, CancellationToken ct) =>
        {
            try
            {
                var sw = Stopwatch.StartNew();
                SelfExplainResponse explanation = await service.ExplainAsync(request, ct);
                sw.Stop();
                return Results.Ok(ApiResponse<SelfExplainResponse>.Ok(explanation, sw.ElapsedMilliseconds));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(ApiResponse<SelfExplainResponse>.Fail($"Error: {ex.Message}"));
            }
        })
        .WithName("SelfExplain")
        .WithTags("Self-Model")
        .WithOpenApi(op =>
        {
            op.Summary = "Generate self-explanation from execution DAG";
            op.Description = "Generates a narrative explanation of the agent's execution history and current state based on the execution DAG.";
            return op;
        })
        .Produces<ApiResponse<SelfExplainResponse>>(200)
        .Produces<ApiResponse<SelfExplainResponse>>(400);

        return app;
    }
}
