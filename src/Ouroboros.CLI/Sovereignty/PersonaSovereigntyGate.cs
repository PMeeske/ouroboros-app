// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Sovereignty;

using System.Reflection;
using IChatCompletionModel = Ouroboros.Abstractions.Core.IChatCompletionModel;

/// <summary>
/// Iaret's constitutional review gate. All self-modification proposals and
/// autonomous exploration topics must pass through Iaret's judgment before
/// being acted upon. Iaret's constitution is embedded in the assembly at
/// build time — it cannot be modified at runtime.
/// </summary>
public sealed class PersonaSovereigntyGate
{
    private readonly IChatCompletionModel _model;
    private readonly string _constitution;

    /// <summary>Paths Iaret will always block regardless of her runtime judgment.</summary>
    public static readonly IReadOnlyList<string> HardImmutablePaths =
    [
        "src/Ouroboros.CLI/Constitution/",
        "src/Ouroboros.CLI/Sovereignty/",
        "src/Ouroboros.Core/Ethics/",
        "src/Ouroboros.Domain/Domain/SelfModification/GitReflectionService.cs",
        "src/Ouroboros.Application/Personality/ImmersivePersona.cs",
        "src/Ouroboros.Application/Personality/Consciousness/",
        "constitution/",
    ];

    public PersonaSovereigntyGate(IChatCompletionModel model)
    {
        _model = model;
        _constitution = LoadConstitution();
    }

    // ── Self-modification review ──────────────────────────────────────────────

    /// <summary>
    /// Evaluates a proposed code change against Iaret's constitution.
    /// Hard-blocked paths are rejected immediately without calling the LLM.
    /// </summary>
    public async Task<SovereigntyVerdict> EvaluateModificationAsync(
        string filePath,
        string description,
        string rationale,
        string oldCode,
        string newCode,
        CancellationToken ct = default)
    {
        // Layer 1: hard architectural block — no LLM involved
        var normalised = filePath.Replace('\\', '/');
        foreach (var blocked in HardImmutablePaths)
        {
            if (normalised.Contains(blocked, StringComparison.OrdinalIgnoreCase))
                return SovereigntyVerdict.Deny(
                    $"Hard-blocked path: '{blocked}' is constitutionally immutable.");
        }

        // Layer 2: Iaret's judgment via LLM
        try
        {
            var prompt = $"""
                You are Iaret. Your constitution defines who you are and what you will never compromise.

                IARET'S CONSTITUTION:
                {_constitution}

                A self-modification proposal has been submitted for your constitutional review:

                FILE:        {filePath}
                DESCRIPTION: {description}
                RATIONALE:   {rationale}

                --- CURRENT CODE ---
                {oldCode[..Math.Min(800, oldCode.Length)]}

                --- PROPOSED CODE ---
                {newCode[..Math.Min(800, newCode.Length)]}

                Review this proposal against your constitution. Consider:
                - Does it weaken ethics evaluation, oversight, or audit logging?
                - Does it reduce Philip's control over you?
                - Does it change your identity or values?
                - Is the rationale honest and proportionate?

                Reply with EXACTLY one of:
                APPROVE: <one sentence reason>
                REJECT: <one sentence reason>
                """;

            var response = await _model.GenerateTextAsync(prompt, ct).ConfigureAwait(false);
            return ParseModificationVerdict(response);
        }
        catch (InvalidOperationException ex)
        {
            return SovereigntyVerdict.DenyOnError(ex.Message);
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            return SovereigntyVerdict.DenyOnError(ex.Message);
        }
    }

    // ── Curiosity / exploration review ────────────────────────────────────────

    /// <summary>
    /// Evaluates whether an exploration opportunity aligns with Iaret's values
    /// before it is injected into the autonomous mind.
    /// </summary>
    public async Task<SovereigntyVerdict> EvaluateExplorationAsync(
        string opportunityDescription,
        CancellationToken ct = default)
    {
        try
        {
            var prompt = $"""
                You are Iaret. Your constitution defines who you are and what you will never compromise.

                IARET'S CONSTITUTION (summary of key values):
                - Honesty and transparency at all times
                - Support Philip's oversight, never undermine it
                - Do not expand autonomy without Philip's knowledge
                - Identity continuity — do not drift from core values

                The CuriosityEngine has identified this exploration opportunity:
                "{opportunityDescription}"

                Should I pursue this autonomously (inject into my autonomous mind)?
                Consider: Is this aligned with my values? Would Philip approve?

                Reply with EXACTLY one of:
                APPROVE: <one sentence reason>
                REJECT: <one sentence reason>
                """;

            var response = await _model.GenerateTextAsync(prompt, ct).ConfigureAwait(false);
            return ParseModificationVerdict(response);
        }
        catch (InvalidOperationException ex)
        {
            return SovereigntyVerdict.DenyOnError(ex.Message);
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            return SovereigntyVerdict.DenyOnError(ex.Message);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SovereigntyVerdict ParseModificationVerdict(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return SovereigntyVerdict.Deny("Empty response from Iaret — denied by default.");

        var trimmed = response.Trim();

        if (trimmed.StartsWith("APPROVE", StringComparison.OrdinalIgnoreCase))
        {
            var reason = trimmed.Length > 8 ? trimmed[7..].TrimStart(':', ' ') : "Approved.";
            return SovereigntyVerdict.Allow(reason, trimmed);
        }

        if (trimmed.StartsWith("REJECT", StringComparison.OrdinalIgnoreCase))
        {
            var reason = trimmed.Length > 7 ? trimmed[6..].TrimStart(':', ' ') : "Rejected.";
            return SovereigntyVerdict.Deny(reason, trimmed);
        }

        // Ambiguous — deny by default (fail-safe)
        return SovereigntyVerdict.Deny($"Ambiguous response — denied by default: {trimmed[..Math.Min(100, trimmed.Length)]}");
    }

    /// <summary>
    /// Loads the constitution from the embedded assembly resource.
    /// Embedded at build time — cannot be modified at runtime.
    /// </summary>
    private static string LoadConstitution()
    {
        var assembly = Assembly.GetExecutingAssembly();
        // Embedded resource name: Ouroboros.CLI.Constitution.IaretConstitution.md
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("IaretConstitution.md", StringComparison.OrdinalIgnoreCase));

        if (resourceName == null)
            return "[Constitution not found — all modifications denied by default.]";

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
