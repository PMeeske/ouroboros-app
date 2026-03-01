// <copyright file="OuroborosAgentPropertyTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using MediatR;
using Moq;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Subsystems;

namespace Ouroboros.Tests.CLI.Commands;

/// <summary>
/// Unit tests verifying that OuroborosAgent property proxies correctly delegate
/// to the injected subsystem instances without mutation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Category", TestCategories.CLI)]
public sealed class OuroborosAgentPropertyTests : IAsyncDisposable
{
    // ── Shared test fixtures ────────────────────────────────────────────────

    private readonly OuroborosConfig _config = new();
    private readonly Mock<IMediator> _mediator = new();

    private readonly VoiceModeService _voiceService;
    private readonly VoiceSubsystem _voiceSub;
    private readonly ModelSubsystem _modelsSub = new();
    private readonly ToolSubsystem _toolsSub = new();
    private readonly MemorySubsystem _memorySub = new();
    private readonly CognitiveSubsystem _cognitiveSub = new();
    private readonly AutonomySubsystem _autonomySub = new();
    private readonly EmbodimentSubsystem _embodimentSub = new();
    private readonly LocalizationSubsystem _localizationSub = new();
    private readonly LanguageSubsystem _languageSub = new();
    private readonly SelfAssemblySubsystem _selfAssemblySub = new();
    private readonly PipeProcessingSubsystem _pipeSub = new();
    private readonly ChatSubsystem _chatSub = new();
    private readonly CommandRoutingSubsystem _commandRoutingSub = new();
    private readonly SwarmSubsystem _swarmSub = new();
    private readonly AuthSubsystem _authSub = new();

    private readonly OuroborosAgent _sut;

    public OuroborosAgentPropertyTests()
    {
        var voiceConfig = new VoiceModeConfig(
            Persona: _config.Persona,
            VoiceOnly: _config.VoiceOnly,
            LocalTts: _config.LocalTts,
            VoiceLoop: true,
            DisableStt: true,
            Model: _config.Model,
            Endpoint: _config.Endpoint,
            EmbedModel: _config.EmbedModel,
            QdrantEndpoint: _config.QdrantEndpoint,
            Culture: _config.Culture);

        _voiceService = new VoiceModeService(voiceConfig);
        _voiceSub = new VoiceSubsystem(_voiceService);

#pragma warning disable CS0618 // Suppress obsolete — we use the DI constructor, not legacy
        _sut = new OuroborosAgent(
            _config,
            _mediator.Object,
            _voiceSub,
            _modelsSub,
            _toolsSub,
            _memorySub,
            _cognitiveSub,
            _autonomySub,
            _embodimentSub,
            _localizationSub,
            _languageSub,
            _selfAssemblySub,
            _pipeSub,
            _chatSub,
            _commandRoutingSub,
            _swarmSub,
            _authSub);
#pragma warning restore CS0618
    }

    public async ValueTask DisposeAsync()
    {
        await _sut.DisposeAsync();
        _voiceService.Dispose();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 1 – SubModels returns the injected IModelSubsystem
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SubModels_Should_ReturnInjectedModelSubsystem()
    {
        // Act
        IModelSubsystem actual = _sut.SubModels;

        // Assert
        actual.Should().BeSameAs(_modelsSub);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2 – SubTools returns the injected IToolSubsystem
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SubTools_Should_ReturnInjectedToolSubsystem()
    {
        // Act
        IToolSubsystem actual = _sut.SubTools;

        // Assert
        actual.Should().BeSameAs(_toolsSub);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3 – SubMemory returns the injected IMemorySubsystem
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SubMemory_Should_ReturnInjectedMemorySubsystem()
    {
        // Act
        IMemorySubsystem actual = _sut.SubMemory;

        // Assert
        actual.Should().BeSameAs(_memorySub);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4 – SubAutonomy returns the injected IAutonomySubsystem
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SubAutonomy_Should_ReturnInjectedAutonomySubsystem()
    {
        // Act
        IAutonomySubsystem actual = _sut.SubAutonomy;

        // Assert
        actual.Should().BeSameAs(_autonomySub);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5 – SubLanguage returns the injected ILanguageSubsystem
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SubLanguage_Should_ReturnInjectedLanguageSubsystem()
    {
        // Act
        ILanguageSubsystem actual = _sut.SubLanguage;

        // Assert
        actual.Should().BeSameAs(_languageSub);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 6 – IsInitialized is false after construction
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void IsInitialized_Should_BeFalse_AfterConstruction()
    {
        // Act
        bool actual = _sut.IsInitialized;

        // Assert
        actual.Should().BeFalse("agent should not be initialized until InitializeAsync completes");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 7 – Skills is initially null (MemorySubsystem default)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Skills_Should_BeNull_WhenMemorySubsystemNotInitialized()
    {
        // Act
        var actual = _sut.Skills;

        // Assert
        actual.Should().BeNull("MemorySubsystem.Skills defaults to null before initialization");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 8 – Personality is initially null (MemorySubsystem default)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Personality_Should_BeNull_WhenMemorySubsystemNotInitialized()
    {
        // Act
        var actual = _sut.Personality;

        // Assert
        actual.Should().BeNull("MemorySubsystem.PersonalityEngine defaults to null before initialization");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 9 – Voice returns VoiceModeService from VoiceSubsystem
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Voice_Should_ReturnVoiceModeServiceFromVoiceSubsystem()
    {
        // Act
        VoiceModeService actual = _sut.Voice;

        // Assert
        actual.Should().BeSameAs(_voiceService);
        actual.Should().BeSameAs(_voiceSub.Service,
            "Voice property must delegate to the VoiceSubsystem.Service instance");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 10 – SetConfiguration stores configuration (verify via reflection)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SetConfiguration_Should_StoreStaticConfiguration()
    {
        // Arrange
        var mockConfig = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();

        // Act
        OuroborosAgent.SetConfiguration(mockConfig.Object);

        // Assert — read back via reflection since _staticConfiguration is private static
        var field = typeof(OuroborosAgent)
            .GetField("_staticConfiguration",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        field.Should().NotBeNull("the backing field should exist");

        var stored = field!.GetValue(null);
        stored.Should().BeSameAs(mockConfig.Object,
            "SetConfiguration should store the provided IConfiguration instance");

        // Cleanup — restore null to avoid leaking state to other tests
        field.SetValue(null, null);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 11 – SetStaticCulture stores culture value
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SetStaticCulture_Should_StoreStaticCultureValue()
    {
        // Arrange
        const string culture = "de-DE";

        // Act
        OuroborosAgent.SetStaticCulture(culture);

        // Assert — read back via reflection since _staticCulture is private static
        var field = typeof(OuroborosAgent)
            .GetField("_staticCulture",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        field.Should().NotBeNull("the backing field should exist");

        var stored = (string?)field!.GetValue(null);
        stored.Should().Be(culture,
            "SetStaticCulture should store the provided culture string");

        // Cleanup — restore null to avoid leaking state to other tests
        field.SetValue(null, null);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Additional proxy delegation tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void InteractionStream_Should_ReturnStreamFromVoiceService()
    {
        // Act
        var actual = _sut.InteractionStream;

        // Assert
        actual.Should().BeSameAs(_voiceService.Stream,
            "InteractionStream must delegate to Voice.Stream");
    }

    [Fact]
    public void PresenceController_Should_ReturnPresenceFromVoiceService()
    {
        // Act
        var actual = _sut.PresenceController;

        // Assert
        actual.Should().BeSameAs(_voiceService.Presence,
            "PresenceController must delegate to Voice.Presence");
    }

    [Fact]
    public void VoiceChannel_Should_BeNull_WhenVoiceSubsystemNotInitialized()
    {
        // Act — VoiceSubsystem.SideChannel defaults to null before InitializeAsync
        var actual = _sut.VoiceChannel;

        // Assert
        actual.Should().BeNull("SideChannel is only created during VoiceSubsystem.InitializeAsync");
    }

    [Fact]
    public void IaretPersona_Should_BeNull_WhenCognitiveSubsystemNotInitialized()
    {
        // Act
        var actual = _sut.IaretPersona;

        // Assert
        actual.Should().BeNull("CognitiveSubsystem.ImmersivePersona defaults to null before initialization");
    }

    [Fact]
    public void AvatarService_Should_BeNull_WhenEmbodimentSubsystemNotInitialized()
    {
        // Act
        var actual = _sut.AvatarService;

        // Assert
        actual.Should().BeNull("EmbodimentSubsystem.AvatarService defaults to null before initialization");
    }

    [Fact]
    public void SetStaticCulture_Should_AcceptNull()
    {
        // Act
        OuroborosAgent.SetStaticCulture(null);

        // Assert — read back via reflection
        var field = typeof(OuroborosAgent)
            .GetField("_staticCulture",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        field.Should().NotBeNull();

        var stored = (string?)field!.GetValue(null);
        stored.Should().BeNull("SetStaticCulture(null) should clear the stored culture");
    }
}
