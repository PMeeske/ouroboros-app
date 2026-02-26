// <copyright file="LegacyOptions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Options;

/// <summary>
/// Legacy Ask options POCO for test compatibility.
/// Maps to <see cref="Ouroboros.CLI.Services.AskRequest"/> in the harness.
/// </summary>
public sealed class AskOptions
{
    public string Question { get; set; } = string.Empty;
    public string Model { get; set; } = "llama3";
    public bool Rag { get; set; }
    public bool Agent { get; set; }
    public string AgentMode { get; set; } = "lc";
    public int AgentMaxSteps { get; set; } = 6;
    public bool Stream { get; set; }
    public bool Debug { get; set; }
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 2048;
    public int TimeoutSeconds { get; set; } = 60;
    public string? Endpoint { get; set; }
    public string? EndpointType { get; set; }
    public string? ApiKey { get; set; }
    public int K { get; set; } = 3;
    public bool JsonTools { get; set; }
    public bool StrictModel { get; set; }
    public string? Culture { get; set; }
    public string Persona { get; set; } = "Iaret";
    public bool Voice { get; set; }
    public bool VoiceOnly { get; set; }
    public bool LocalTts { get; set; } = true;
    public bool VoiceLoop { get; set; }
    public string Router { get; set; } = "direct";
    public string? CoderModel { get; set; }
    public string? SummarizeModel { get; set; }
    public string? ReasonModel { get; set; }
    public string? GeneralModel { get; set; }
    public string Embed { get; set; } = "nomic-embed-text";
    public string EmbedModel { get; set; } = "nomic-embed-text";
}

/// <summary>
/// Legacy Explain options POCO for test compatibility.
/// </summary>
public sealed class ExplainOptions
{
    public string Dsl { get; set; } = string.Empty;
}

/// <summary>
/// Legacy Test options POCO for test compatibility.
/// Maps to <see cref="Ouroboros.CLI.Mediator.RunTestRequest"/> test spec in the harness.
/// </summary>
public sealed class TestOptions
{
    public bool All { get; set; }
    public bool IntegrationOnly { get; set; }
    public bool CliOnly { get; set; }
    public bool MeTTa { get; set; }
}

/// <summary>
/// Legacy MeTTa options POCO for test compatibility.
/// Maps to <see cref="Ouroboros.CLI.Commands.MeTTaConfig"/> in the harness.
/// </summary>
public sealed class MeTTaOptions
{
    public string Goal { get; set; } = string.Empty;
    public string Model { get; set; } = "llama3";
    public bool PlanOnly { get; set; }
    public bool ShowMetrics { get; set; } = true;
    public bool Interactive { get; set; }
    public bool Debug { get; set; }
    public string? Culture { get; set; }
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 512;
    public int TimeoutSeconds { get; set; } = 60;
    public string Embed { get; set; } = "nomic-embed-text";
    public string EmbedModel { get; set; } = "nomic-embed-text";
    public bool Voice { get; set; }
    public bool VoiceOnly { get; set; }
    public bool LocalTts { get; set; }
    public bool VoiceLoop { get; set; }
    public string Persona { get; set; } = "Iaret";
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public string? EndpointType { get; set; }
    public string QdrantEndpoint { get; set; } = "http://localhost:6334";
}
