// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Subsystems;

using Ouroboros.Application.Avatar;
using Ouroboros.Application.Personality;
using Ouroboros.Application.Services;
using Ouroboros.CLI.Avatar;

/// <summary>
/// Self-contained subsystem for avatar rendering and persona event wiring.
///
/// Both <see cref="Commands.ImmersiveMode"/> and <see cref="Commands.OuroborosAgent"/> delegate
/// all avatar lifecycle and thought-bridging logic here rather than duplicating it inline.
///
/// Pattern mirrors OuroborosAgent.WirePersonaEvents / WireAvatarMoodTransitions.
/// </summary>
public sealed class ImmersiveSubsystem : IImmersiveSubsystem
{
    public string Name => "Immersive";
    public bool IsInitialized { get; private set; }

    public InteractiveAvatarService? AvatarService { get; set; }

    private int _avatarPort;
    private string _personaName = "Iaret";

    // Dedup: prevent the same thought being printed multiple times when several
    // event handlers (OuroborosAgent + ImmersiveSubsystem) subscribe to the same persona.
    private static string? _lastThoughtContent;
    private static DateTime _lastThoughtTime = DateTime.MinValue;

    /// <summary>
    /// Configures the subsystem before calling <see cref="InitializeAsync"/>.
    /// </summary>
    public void Configure(string personaName, int avatarPort = 0)
    {
        _personaName = personaName;
        _avatarPort = avatarPort;
    }

    /// <inheritdoc/>
    public async Task InitializeAsync(SubsystemInitContext ctx)
    {
        _personaName = ctx.VoiceService.ActivePersona?.Name ?? _personaName;

        // Read avatar port from config if not already configured
        if (_avatarPort == 0)
            _avatarPort = ctx.Config.AvatarPort;

        if (ctx.Config.Avatar)
        {
            try
            {
                ctx.Output.WriteSystem("  [~] Launching avatar viewer...");
                var (service, _) = await AvatarIntegration.CreateAndStartAsync(
                    _personaName, _avatarPort, ct: CancellationToken.None);
                AvatarService = service;
                ctx.Output.WriteSystem("  [OK] Avatar viewer launched — Iaret is watching");
            }
            catch (Exception ex)
            {
                ctx.Output.WriteWarning($"Avatar launch failed: {ex.Message}");
            }
        }

        IsInitialized = true;
    }

    /// <inheritdoc/>
    public async Task InitializeStandaloneAsync(
        string personaName, bool avatarEnabled, int avatarPort,
        CancellationToken ct = default)
    {
        _personaName = personaName;
        _avatarPort = avatarPort;

        if (avatarEnabled)
        {
            try
            {
                Console.WriteLine("  [~] Launching interactive avatar...");
                var (service, _) = await AvatarIntegration.CreateAndStartAsync(
                    personaName, avatarPort, ct: ct);
                AvatarService = service;
                Console.WriteLine("  [OK] Avatar viewer launched — Iaret is watching");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [!] Avatar launch failed: {ex.Message}");
            }
        }

        IsInitialized = true;
    }

    /// <inheritdoc/>
    public void WirePersonaEvents(ImmersivePersona persona, AutonomousMind? mind = null)
    {
        // Mirror OuroborosAgent.WirePersonaEvents — only surface genuine LLM thoughts.
        // Metacognitive and Musing types are template strings ("I notice that I tend to {0}")
        // and carry no real insight — skip those.
        persona.AutonomousThought += (_, e) =>
        {
            if (e.Thought.Type is not (InnerThoughtType.Curiosity
                                    or InnerThoughtType.Observation
                                    or InnerThoughtType.SelfReflection))
                return;

            var content = e.Thought.Content;

            // Filter: skip empty, very short, or unresolved template placeholders.
            // Template artifacts look like "[Symbolic context: inference-available: ..." where
            // a bracket-enclosed tag was never substituted with real content by the engine.
            if (string.IsNullOrWhiteSpace(content) || content.Length < 12)
                return;
            var bracketIdx = content.IndexOf('[');
            if (bracketIdx >= 0 && content.IndexOf(':', bracketIdx) > bracketIdx)
                return;

            // Dedup: multiple handlers (OuroborosAgent + ImmersiveSubsystem) can subscribe
            // to the same ImmersivePersona instance. Skip if the same thought printed < 8s ago.
            var now = DateTime.UtcNow;
            if (content == _lastThoughtContent && (now - _lastThoughtTime).TotalSeconds < 8)
            {
                // Still wire avatar even if we skip the console print
                if (AvatarService is { } avatarSvc)
                    avatarSvc.NotifyMoodChange(avatarSvc.CurrentState.Mood, avatarSvc.CurrentState.Energy,
                                               avatarSvc.CurrentState.Positivity, content);
                return;
            }

            _lastThoughtContent = content;
            _lastThoughtTime = now;

            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine($"\n  💭 {content}");
            Console.ResetColor();

            if (AvatarService is { } svc)
                svc.NotifyMoodChange(svc.CurrentState.Mood, svc.CurrentState.Energy,
                                     svc.CurrentState.Positivity, content);
        };

        // Mirror OuroborosAgent.WireAvatarMoodTransitions
        persona.ConsciousnessShift += (_, e) =>
        {
            AvatarService?.NotifyMoodChange(
                e.NewEmotion ?? "neutral",
                0.5 + (e.ArousalChange * 0.5),
                e.NewEmotion?.Contains("warm") == true || e.NewEmotion?.Contains("gentle") == true ? 0.8 : 0.5);
        };

        // AutonomousMind thought stream → avatar status
        if (mind != null)
        {
            mind.OnProactiveMessage += (msg) =>
            {
                var shortMsg = msg?.Length > 60 ? msg[..57] + "..." : msg;
                AvatarService?.NotifyMoodChange("warm", 0.7, 0.8, shortMsg);
            };

            mind.OnThought += (thought) =>
            {
                if (!string.IsNullOrWhiteSpace(thought.Content) && thought.Content.Length > 10)
                {
                    var shortThought = thought.Content.Length > 60 ? thought.Content[..57] + "..." : thought.Content;
                    AvatarService?.NotifyMoodChange("contemplative", 0.4, 0.5, shortThought);
                }
            };

            mind.OnEmotionalChange += (emotion) =>
            {
                AvatarService?.NotifyMoodChange(
                    emotion.DominantEmotion,
                    0.5 + (emotion.Arousal * 0.3),
                    emotion.Valence > 0 ? 0.7 : 0.4,
                    emotion.Description);
            };
        }
    }

    /// <inheritdoc/>
    public void PushTopicHint(string rawInput)
    {
        if (AvatarService == null) return;
        var topic = ClassifyAvatarTopic(rawInput);
        if (!string.IsNullOrEmpty(topic))
            AvatarService.SetTopicHint(topic);
    }

    /// <inheritdoc/>
    public void SetPresenceState(string presenceState, string mood, double energy = 0.5, double positivity = 0.5)
        => AvatarService?.SetPresenceState(presenceState, mood, energy, positivity);

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (AvatarService != null)
        {
            await AvatarService.DisposeAsync();
            AvatarService = null;
        }
    }

    // ── Topic classification ──────────────────────────────────────────────────────
    // Maps user input keywords → TOPIC_POSITIONS keys in avatar.html.
    // Kept here (single source of truth) instead of duplicated in each command.

    /// <summary>
    /// Maps user input to an avatar topic category for stage positioning + expression flash.
    /// Returns a key from <c>TOPIC_POSITIONS</c> in <c>avatar.html</c>, or empty string.
    /// </summary>
    public static string ClassifyAvatarTopic(string input)
    {
        var l = input.ToLowerInvariant();
        bool Has(params string[] words) => words.Any(w => l.Contains(w, StringComparison.Ordinal));

        if (Has("code", "function", "class", "bug", "error", "compile", "debug", "algorithm", "implement", "program"))
            return "code";
        if (Has("math", "equation", "formula", "calculate", "statistics", "probability", "geometry", "algebra"))
            return "mathematical";
        if (Has("science", "physics", "chemistry", "biology", "neural", "quantum", "technical", "architecture"))
            return "technical";
        if (Has("analyze", "analyse", "compare", "evaluate", "assess", "review", "examine"))
            return "analytical";
        if (Has("feel", "feeling", "emotion", "heart", "hurt", "love", "hate", "sad", "angry", "anxious", "lonely"))
            return "emotional";
        if (Has("help me", "support", "struggling", "hard time", "cant cope", "worried about"))
            return "supportive";
        if (Has("empathy", "understand me", "listen", "relate"))
            return "empathetic";
        if (Has("philosophy", "ethics", "meaning", "purpose", "existence", "consciousness", "reality", "truth"))
            return "philosophical";
        if (Has("think about", "reflect", "wonder", "ponder", "contemplate", "abstract", "concept"))
            return "abstract";
        if (Has("myself", "introspect", "inner self", "who am i", "my identity"))
            return "introspective";
        if (Has("imagine", "creative", "story", "design", "art", "music", "write", "invent"))
            return "creative";
        if (Has("fun", "joke", "laugh", "play", "game", "silly", "humor", "funny"))
            return "playful";
        if (Has("debate", "argue", "disagree", "challenge", "wrong", "prove"))
            return "confrontational";
        if (Has("what do you think", "your opinion", "tell me", "explain", "discuss"))
            return "engaging";

        return string.Empty;
    }
}
