// <copyright file="Program.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Microsoft.Extensions.Configuration;
using Ouroboros.ApiHost.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Load user secrets so API keys and other sensitive config are never in appsettings.json.
builder.Configuration.AddUserSecrets(System.Reflection.Assembly.GetEntryAssembly()!, optional: true);

// Register all Ouroboros Web API services via the shared ApiHost extension.
// The same extension is used when co-hosting inside the CLI (--serve) or Android.
builder.Services.AddOuroborosWebApi(builder.Configuration);

WebApplication app = builder.Build();

// Apply middleware pipeline (correlation ID, exception handler, Swagger, CORS)
app.UseOuroborosWebApi();

// Map all Ouroboros API endpoints (/health, /ready, /api/ask, /api/pipeline, /api/self/*)
app.MapOuroborosApiEndpoints();

app.Run();

// Make Program class accessible for WebApplicationFactory testing
public partial class Program
{
}
