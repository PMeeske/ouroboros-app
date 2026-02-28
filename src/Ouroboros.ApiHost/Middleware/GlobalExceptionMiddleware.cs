using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ouroboros.ApiHost.Middleware;

using System.Net;
using System.Text.Json;
using Ouroboros.Application.Json;

/// <summary>
/// Catches unhandled exceptions and returns a consistent RFC 7807 Problem Details
/// response instead of leaking stack traces or returning opaque 500 errors.
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var correlationId = context.Items["CorrelationId"]?.ToString();
            _logger.LogError(ex, "Unhandled exception processing {Method} {Path} (CorrelationId: {CorrelationId})",
                context.Request.Method, context.Request.Path, correlationId);

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/problem+json";

            var problem = new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                title = "An unexpected error occurred",
                status = 500,
                detail = _env.IsDevelopment() ? ex.Message : "An internal server error occurred. Check logs for details.",
                instance = context.Request.Path.ToString(),
                correlationId,
            };

            var json = JsonSerializer.Serialize(problem, JsonDefaults.Compact);
            await context.Response.WriteAsync(json);
        }
    }
}
