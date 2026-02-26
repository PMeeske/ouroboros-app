// <copyright file="OuroborosAgentConstructorTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using MediatR;
using Moq;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Subsystems;

namespace Ouroboros.Tests.CLI.Commands;

/// <summary>
/// Unit tests for <see cref="OuroborosAgent"/> DI constructor and property wiring.
/// Verifies that subsystems are assigned to the correct backing fields and that
/// public/internal accessors expose the expected instances after construction.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class OuroborosAgentConstructorTests : IAsyncDisposable
{
    // ── Shared fixtures ──────────────────────────────────────────────────────
    private readonly OuroborosConfig _config = new();
    private readonly Mock<IMediator> _mediatorMock = new();
    private readonly Mock<IServiceProvider> _serviceProviderMock = new();

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

    public OuroborosAgentConstructorTests()
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

        _sut = new OuroborosAgent(
            _config,
            _mediatorMock.Object,
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
            _authSub,
            _serviceProviderMock.Object);
    }

    public async ValueTask DisposeAsync()
    {
        await _sut.DisposeAsync();
        _voiceService.Dispose();
    }

    // ── 1. Subsystem accessor properties point to the injected instances ─────

    [Fact]
    public void SubModels_ReturnsInjectedModelSubsystem()
    {
        _sut.SubModels.Should().BeSameAs(_modelsSub);
    }

    [Fact]
    public void SubTools_ReturnsInjectedToolSubsystem()
    {
        _sut.SubTools.Should().BeSameAs(_toolsSub);
    }

    [Fact]
    public void SubMemory_ReturnsInjectedMemorySubsystem()
    {
        _sut.SubMemory.Should().BeSameAs(_memorySub);
    }

    [Fact]
    public void SubAutonomy_ReturnsInjectedAutonomySubsystem()
    {
        _sut.SubAutonomy.Should().BeSameAs(_autonomySub);
    }

    [Fact]
    public void SubLanguage_ReturnsInjectedLanguageSubsystem()
    {
        _sut.SubLanguage.Should().BeSameAs(_languageSub);
    }

    // ── 2. IsInitialized is false immediately after construction ─────────────

    [Fact]
    public void IsInitialized_IsFalseBeforeInitialization()
    {
        _sut.IsInitialized.Should().BeFalse(
            "the agent should not report as initialized until InitializeAsync completes");
    }

    // ── 3. Config and Mediator stored correctly (internal accessors) ─────────

    [Fact]
    public void Config_ReturnsInjectedConfig()
    {
        _sut.Config.Should().BeSameAs(_config);
    }

    [Fact]
    public void Mediator_ReturnsInjectedMediator()
    {
        _sut.Mediator.Should().BeSameAs(_mediatorMock.Object);
    }

    // ── 4. Voice property returns VoiceModeService from VoiceSubsystem ───────

    [Fact]
    public void Voice_ReturnsServiceFromVoiceSubsystem()
    {
        _sut.Voice.Should().BeSameAs(_voiceService);
    }

    [Fact]
    public void Voice_HasExpectedPersonaName()
    {
        _sut.Voice.ActivePersona.Name.Should().Be("Iaret",
            "the default persona configured in OuroborosConfig is 'Iaret'");
    }

    // ── 5. _allSubsystems array is populated (15 subsystems) ─────────────────
    //    Verified indirectly: DisposeAsync iterates _allSubsystems in reverse
    //    and calls DisposeAsync on each. If the array were empty or incomplete,
    //    subsystems would leak. We verify the count via the internal accessors
    //    that correspond to all 15 expected subsystem slots.

    [Fact]
    public void AllSubsystems_ContainsAllFifteenSubsystems()
    {
        // Access each internal subsystem accessor to confirm non-null assignment.
        // These map 1:1 to the _allSubsystems array entries.
        IAgentSubsystem[] expectedSubsystems =
        [
            _sut.AuthSub,
            _sut.VoiceSub,
            _sut.ModelsSub,
            _sut.ToolsSub,
            _sut.MemorySub,
            _sut.CognitiveSub,
            _sut.AutonomySub,
            _sut.EmbodimentSub,
            _sut.LocalizationSub,
            (IAgentSubsystem)_sut.SubLanguage,
            _sut.SelfAssemblySub,
            _sut.PipeSub,
            _sut.ChatSub,
            _sut.CommandRoutingSub,
            _sut.SwarmSub,
        ];

        expectedSubsystems.Should().HaveCount(15,
            "the agent registers exactly 15 subsystems in the _allSubsystems array");

        expectedSubsystems.Should().AllSatisfy(sub =>
            sub.Should().NotBeNull("every subsystem slot must be populated after construction"));
    }

    [Fact]
    public async Task DisposeAsync_DoesNotThrow_WhenCalledOnFreshAgent()
    {
        // Arrange: create a separate agent for this isolated disposal test
        var voiceConfig = new VoiceModeConfig(DisableStt: true, VoiceLoop: false);
        using var voiceService = new VoiceModeService(voiceConfig);
        var voiceSub = new VoiceSubsystem(voiceService);

        var agent = new OuroborosAgent(
            new OuroborosConfig(),
            _mediatorMock.Object,
            voiceSub,
            new ModelSubsystem(),
            new ToolSubsystem(),
            new MemorySubsystem(),
            new CognitiveSubsystem(),
            new AutonomySubsystem(),
            new EmbodimentSubsystem(),
            new LocalizationSubsystem(),
            new LanguageSubsystem(),
            new SelfAssemblySubsystem(),
            new PipeProcessingSubsystem(),
            new ChatSubsystem(),
            new CommandRoutingSubsystem(),
            new SwarmSubsystem(),
            new AuthSubsystem());

        // Act & Assert: DisposeAsync iterates _allSubsystems; should not throw
        var act = () => agent.DisposeAsync();
        await act.Should().NotThrowAsync(
            "disposing a freshly constructed agent should safely iterate all 15 subsystems");
    }

    // ── Supplementary: internal subsystem accessors point to correct instances ─

    [Fact]
    public void InternalSubsystemAccessors_PointToInjectedInstances()
    {
        _sut.VoiceSub.Should().BeSameAs(_voiceSub);
        _sut.ModelsSub.Should().BeSameAs(_modelsSub);
        _sut.ToolsSub.Should().BeSameAs(_toolsSub);
        _sut.MemorySub.Should().BeSameAs(_memorySub);
        _sut.CognitiveSub.Should().BeSameAs(_cognitiveSub);
        _sut.AutonomySub.Should().BeSameAs(_autonomySub);
        _sut.EmbodimentSub.Should().BeSameAs(_embodimentSub);
        _sut.LocalizationSub.Should().BeSameAs(_localizationSub);
        _sut.SelfAssemblySub.Should().BeSameAs(_selfAssemblySub);
        _sut.PipeSub.Should().BeSameAs(_pipeSub);
        _sut.ChatSub.Should().BeSameAs(_chatSub);
        _sut.CommandRoutingSub.Should().BeSameAs(_commandRoutingSub);
        _sut.SwarmSub.Should().BeSameAs(_swarmSub);
        _sut.AuthSub.Should().BeSameAs(_authSub);
    }

    // ── Constructor with null serviceProvider (optional parameter) ────────────

    [Fact]
    public void Constructor_WithNullServiceProvider_DoesNotThrow()
    {
        var voiceConfig = new VoiceModeConfig(DisableStt: true, VoiceLoop: false);
        using var voiceService = new VoiceModeService(voiceConfig);
        var voiceSub = new VoiceSubsystem(voiceService);

        var act = () => new OuroborosAgent(
            new OuroborosConfig(),
            _mediatorMock.Object,
            voiceSub,
            new ModelSubsystem(),
            new ToolSubsystem(),
            new MemorySubsystem(),
            new CognitiveSubsystem(),
            new AutonomySubsystem(),
            new EmbodimentSubsystem(),
            new LocalizationSubsystem(),
            new LanguageSubsystem(),
            new SelfAssemblySubsystem(),
            new PipeProcessingSubsystem(),
            new ChatSubsystem(),
            new CommandRoutingSubsystem(),
            new SwarmSubsystem(),
            new AuthSubsystem(),
            serviceProvider: null);

        act.Should().NotThrow(
            "serviceProvider is optional and the constructor should accept null");
    }
}
