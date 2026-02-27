// <copyright file="EnvironmentTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.SystemTools;

using System.Text;
using System.Text.Json;

/// <summary>
/// Get/set environment variables with secret redaction.
/// </summary>
internal class EnvironmentTool : ITool
{
    public string Name => "environment";
    public string Description => "Get/set environment variables. Input: JSON {\"action\":\"get|set|list\", \"name\":\"PATH\", \"value\":\"...\"}";
    public string? JsonSchema => null;

    /// <summary>
    /// Substrings in environment variable names that indicate secrets.
    /// Values for matching variables are redacted in <c>list</c> output.
    /// </summary>
    private static readonly string[] SecretPatterns =
    {
        "KEY", "SECRET", "TOKEN", "PASSWORD", "CREDENTIAL", "APIKEY"
    };

    public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(input) || input.Trim() == "list")
            {
                return Task.FromResult(ListEnvironmentVariables());
            }

            var args = JsonSerializer.Deserialize<JsonElement>(input);
            var action = args.TryGetProperty("action", out var actEl) ? actEl.GetString() ?? "get" : "get";
            var name = args.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";

            return action.ToLower() switch
            {
                "get" => Task.FromResult(Result<string, string>.Success(
                    Environment.GetEnvironmentVariable(name) ?? $"[{name} not set]")),
                "set" when args.TryGetProperty("value", out var valEl) =>
                    SetEnvVar(name, valEl.GetString() ?? ""),
                "list" => Task.FromResult(ListEnvironmentVariables()),
                _ => Task.FromResult(Result<string, string>.Failure($"Unknown action: {action}"))
            };
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<string, string>.Failure(ex.Message));
        }
    }

    private static Result<string, string> ListEnvironmentVariables()
    {
        var vars = Environment.GetEnvironmentVariables();
        var sb = new StringBuilder();
        foreach (System.Collections.DictionaryEntry entry in vars)
        {
            var name = entry.Key?.ToString() ?? "";
            var value = entry.Value?.ToString() ?? "";

            if (IsSecret(name))
            {
                value = "****";
            }
            else if (value.Length > 100)
            {
                value = value[..100] + "...";
            }

            sb.AppendLine($"{name}={value}");
        }
        return Result<string, string>.Success(sb.ToString());
    }

    private static bool IsSecret(string name)
    {
        return SecretPatterns.Any(p => name.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private static Task<Result<string, string>> SetEnvVar(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value);
        return Task.FromResult(Result<string, string>.Success($"Set {name}={value}"));
    }
}
