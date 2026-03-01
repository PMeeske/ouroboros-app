using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ouroboros.ApiHost.Middleware;

/// <summary>
/// Ensures every request/response carries a correlation ID for distributed tracing.
/// If the caller sends <c>X-Correlation-ID</c>, that value is reused; otherwise a new
/// GUID is generated. The ID is pushed into the log scope so structured logs include it.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-ID";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var rawCorrelationId = context.Request.Headers[HeaderName].FirstOrDefault();
        // Sanitize caller-supplied correlation IDs to prevent log injection and header injection
        var correlationId = rawCorrelationId != null
            && rawCorrelationId.Length <= 64
            && System.Text.RegularExpressions.Regex.IsMatch(rawCorrelationId, @"^[a-zA-Z0-9\-_\.]+$")
            ? rawCorrelationId
            : Guid.NewGuid().ToString("N");

        context.Items["CorrelationId"] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await _next(context);
        }
    }
}
