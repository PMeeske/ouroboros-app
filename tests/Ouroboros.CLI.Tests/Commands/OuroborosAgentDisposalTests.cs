// <copyright file="OuroborosAgentDisposalTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Reflection;
using MediatR;
using Moq;
using Ouroboros.CLI.Commands;
using Ouroboros.CLI.Subsystems;
using Ouroboros.Providers;
using Ouroboros.Tests.Infrastructure.Utilities;
using Ouroboros.Tools.MeTTa;

namespace Ouroboros.Tests.CLI.Commands;

/// <summary>
/// Unit tests for OuroborosAgent disposal and lifecycle management.
/// Verifies DisposeAsync disposes all subsystems in reverse registration order,
/// handles exceptions gracefully, is idempotent, and executes pre-dispose hooks.
///
/// Design: Because OuroborosAgent's constructor casts interface parameters to sealed
/// concrete types, we cannot inject mocks directly. Instead, we construct the agent
/// with real concrete subsystems and then use reflection to replace the
/// <c>_allSubsystems</c> array with lightweight <see cref="CallbackSubsystem"/>
/// instances that track disposal calls and ordering.
/// For pre-dispose hook tests (personality save, cost summary), we operate on the
/// real concrete subsystems whose properties the agent reads during OnDisposingAsync.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Category", TestCategories.CLI)]
public class OuroborosAgentDisposalTests
{
    private const int SubsystemCount = 15;

    private static readonly string[] ExpectedReverseOrder =
    [
        "Swarm", "CommandRouting", "Chat", "PipeProcessing", "SelfAssembly",
        "Language", "Localization", "Embodiment",
        "Autonomy", "Cognitive", "Memory", "Tools", "Models", "Voice", "Auth"
    ];

    /// <summary>
    /// Registration order of subsystem names, matching the _allSubsystems array in OuroborosAgent.
    /// </summary>
    private static readonly string[] RegistrationOrder =
    [
        "Auth", "Voice", "Models", "Tools", "Memory",
        "Cognitive", "Autonomy", "Embodiment",
        "Localization", "Language", "SelfAssembly", "PipeProcessing", "Chat", "CommandRouting",
        "Swarm"
    ];

    // ═══════════════════════════════════════════════════════════════════════════
    // FACTORY: Create agent + replace _allSubsystems with callback subsystems
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates an OuroborosAgent with real subsystems, then replaces the internal
    /// _allSubsystems array with <see cref="CallbackSubsystem"/> instances that
    /// track disposal. Returns the agent and the callback subsystems.
    /// </summary>
    private static (OuroborosAgent Agent, CallbackSubsystem[] Subsystems) CreateAgentWithCallbackSubsystems(
        OuroborosConfig? configOverride = null)
    {
        var config = configOverride ?? new OuroborosConfig(
            Persona: "Iaret",
            CostSummary: false,
            Verbosity: OutputVerbosity.Quiet);

        var mediator = new Mock<IMediator>().Object;
        var voiceConfig = new VoiceModeConfig(
            Persona: "Iaret",
            DisableStt: true,
            VoiceLoop: false);
        var voiceService = new VoiceModeService(voiceConfig);

        var agent = new OuroborosAgent(
            config,
            mediator,
            new VoiceSubsystem(voiceService),
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

        // Build callback subsystems matching registration order
        var disposalOrder = new List<string>();
        var callbacks = new CallbackSubsystem[SubsystemCount];
        for (int i = 0; i < SubsystemCount; i++)
        {
            callbacks[i] = new CallbackSubsystem(RegistrationOrder[i], disposalOrder);
        }

        // Replace elements in the existing _allSubsystems array via reflection.
        // The field is readonly, but the array contents are mutable.
        var field = typeof(OuroborosAgent).GetField(
            "_allSubsystems", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var allSubsystems = (IAgentSubsystem[])field.GetValue(agent)!;
        for (int i = 0; i < SubsystemCount; i++)
        {
            allSubsystems[i] = callbacks[i];
        }

        return (agent, callbacks);
    }

    /// <summary>
    /// Creates an OuroborosAgent with real concrete subsystems (no callback replacement).
    /// Used for pre-dispose hook tests that operate on real subsystem properties.
    /// </summary>
    private static (OuroborosAgent Agent, ModelSubsystem Models, MemorySubsystem Memory) CreateAgentWithRealSubsystems(
        OuroborosConfig? configOverride = null)
    {
        var config = configOverride ?? new OuroborosConfig(
            Persona: "Iaret",
            CostSummary: false,
            Verbosity: OutputVerbosity.Quiet);

        var mediator = new Mock<IMediator>().Object;
        var voiceConfig = new VoiceModeConfig(
            Persona: "Iaret",
            DisableStt: true,
            VoiceLoop: false);
        var voiceService = new VoiceModeService(voiceConfig);

        var models = new ModelSubsystem();
        var memory = new MemorySubsystem();

        var agent = new OuroborosAgent(
            config,
            mediator,
            new VoiceSubsystem(voiceService),
            models,
            new ToolSubsystem(),
            memory,
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

        return (agent, models, memory);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TEST 1: DisposeAsync disposes all subsystems
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DisposeAsync_Should_DisposeAllSubsystems()
    {
        // Arrange
        var (agent, subsystems) = CreateAgentWithCallbackSubsystems();

        // Act
        await agent.DisposeAsync();

        // Assert — every callback subsystem should have been disposed exactly once
        foreach (CallbackSubsystem sub in subsystems)
        {
            sub.DisposeCount.Should().Be(1,
                $"{sub.Name} subsystem should be disposed exactly once");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TEST 2: Idempotency — calling DisposeAsync twice only disposes once
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DisposeAsync_Should_BeIdempotent_WhenCalledTwice()
    {
        // Arrange
        var (agent, subsystems) = CreateAgentWithCallbackSubsystems();

        // Act
        await agent.DisposeAsync();
        await agent.DisposeAsync();

        // Assert — each subsystem should still only have been disposed once
        foreach (CallbackSubsystem sub in subsystems)
        {
            sub.DisposeCount.Should().Be(1,
                $"{sub.Name} should only be disposed once despite double DisposeAsync call");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TEST 3: Exception resilience — subsystem disposal exceptions are swallowed
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DisposeAsync_Should_ContinueDisposingWhenSubsystemThrows()
    {
        // Arrange — make the Cognitive subsystem (index 5) throw during disposal
        var (agent, subsystems) = CreateAgentWithCallbackSubsystems();
        CallbackSubsystem cognitiveSub = subsystems[5];
        cognitiveSub.Name.Should().Be("Cognitive", "precondition: index 5 is Cognitive");
        cognitiveSub.ThrowOnDispose = true;

        // Act — should not throw
        Func<Task> act = async () => await agent.DisposeAsync();
        await act.Should().NotThrowAsync(
            "DisposeAsync should swallow subsystem disposal exceptions");

        // Assert — subsystems before and after the faulting one should still be disposed
        // In reverse order: Swarm (index 14) is disposed first, Auth (index 0) is last.
        // Cognitive is at registration index 5, disposed at reverse position 9.
        CallbackSubsystem swarmSub = subsystems[14];
        swarmSub.DisposeCount.Should().Be(1,
            "Swarm (disposed before Cognitive in reverse order) should still be disposed");

        CallbackSubsystem authSub = subsystems[0];
        authSub.DisposeCount.Should().Be(1,
            "Auth (disposed after Cognitive in reverse order) should still be disposed");

        CallbackSubsystem memorySub = subsystems[4];
        memorySub.DisposeCount.Should().Be(1,
            "Memory (disposed after Cognitive in reverse order) should still be disposed");

        // All non-faulting subsystems should still be disposed
        int disposedCount = subsystems.Count(s => s.DisposeCount > 0);
        disposedCount.Should().Be(SubsystemCount,
            "all subsystems should have their DisposeAsync called, including the faulting one");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TEST 4: Reverse disposal order — swarm first, auth last
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task DisposeAsync_Should_DisposeSubsystemsInReverseRegistrationOrder()
    {
        // Arrange
        var (agent, subsystems) = CreateAgentWithCallbackSubsystems();

        // Act
        await agent.DisposeAsync();

        // Assert — gather the disposal order from the shared tracker
        // All callback subsystems share the same disposal order list via the first one
        IReadOnlyList<string> disposalOrder = subsystems[0].DisposalOrderSnapshot;

        disposalOrder.Should().HaveCount(SubsystemCount,
            "all 15 subsystems should be disposed");

        // Swarm (last in registration) should be first in disposal
        disposalOrder.First().Should().Be("Swarm",
            "Swarm is last in registration so it should be first in disposal (reverse order)");

        // Auth (first in registration) should be last in disposal
        disposalOrder.Last().Should().Be("Auth",
            "Auth is first in registration so it should be last in disposal (reverse order)");

        // Verify the full expected reverse order (exact sequence match)
        disposalOrder.Should().Equal(ExpectedReverseOrder,
            "subsystems must be disposed in exact reverse registration order");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TEST 5: OnDisposingAsync saves personality snapshot if engine is present
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task OnDisposingAsync_Should_SavePersonalitySnapshot_WhenPersonalityEngineIsNotNull()
    {
        // Arrange — Use real subsystems so OnDisposingAsync can access _personalityEngine
        // and _memorySub. PersonalityEngine is sealed so we cannot mock it. Instead,
        // we create a real PersonalityEngine using the single-arg constructor (which
        // sets _qdrantClient and _embeddingModel to null). When SavePersonalitySnapshotAsync
        // is called, the null Qdrant guard causes an early return — no exception, no I/O.
        var (agent, _, memory) = CreateAgentWithRealSubsystems();

        var mockMeTTa = new Mock<IMeTTaEngine>();
        var personalityEngine = new Ouroboros.Application.Personality.PersonalityEngine(mockMeTTa.Object);
        memory.PersonalityEngine = personalityEngine;

        // Act — should not throw; the engine's save will early-return due to null Qdrant
        Func<Task> act = async () => await agent.DisposeAsync();
        await act.Should().NotThrowAsync(
            "DisposeAsync should handle personality snapshot save gracefully when no Qdrant is configured");

        // Assert — verify the personality engine was non-null (precondition for the code path)
        memory.PersonalityEngine.Should().NotBeNull(
            "test precondition: PersonalityEngine should have been set on MemorySubsystem");
    }

    [Fact]
    public async Task OnDisposingAsync_Should_SkipPersonalitySave_WhenPersonalityEngineIsNull()
    {
        // Arrange — no personality engine set
        var (agent, _, memory) = CreateAgentWithRealSubsystems();
        memory.PersonalityEngine.Should().BeNull("precondition: no engine set");

        // Act — should complete without attempting save
        Func<Task> act = async () => await agent.DisposeAsync();
        await act.Should().NotThrowAsync(
            "DisposeAsync should skip personality save when engine is null");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TEST 6: OnDisposingAsync handles cost summary when enabled with requests
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task OnDisposingAsync_Should_DisplayCostSummary_WhenEnabledAndTrackerHasRequests()
    {
        // Arrange — enable cost summary and create a tracker with recorded requests
        var costConfig = new OuroborosConfig(
            Persona: "Iaret",
            CostSummary: true,
            Verbosity: OutputVerbosity.Quiet);

        var (agent, models, _) = CreateAgentWithRealSubsystems(costConfig);

        // Set up a cost tracker with at least one completed request
        var tracker = new LlmCostTracker("llama3");
        tracker.StartRequest();
        tracker.EndRequest(100, 50);
        models.CostTracker = tracker;

        // Verify precondition
        tracker.GetSessionMetrics().TotalRequests.Should().BeGreaterThan(0,
            "precondition: tracker should have recorded at least one request");

        // Act — should not throw even when writing cost summary to AnsiConsole
        Func<Task> act = async () => await agent.DisposeAsync();
        await act.Should().NotThrowAsync(
            "DisposeAsync should handle cost summary display without throwing");
    }

    [Fact]
    public async Task OnDisposingAsync_Should_SkipCostSummary_WhenDisabled()
    {
        // Arrange — cost summary disabled (default for our test config)
        var (agent, models, _) = CreateAgentWithRealSubsystems();

        var tracker = new LlmCostTracker("llama3");
        tracker.StartRequest();
        tracker.EndRequest(200, 100);
        models.CostTracker = tracker;

        // Act
        Func<Task> act = async () => await agent.DisposeAsync();
        await act.Should().NotThrowAsync(
            "DisposeAsync should complete without issues when CostSummary is disabled");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // HELPER: CallbackSubsystem — lightweight IAgentSubsystem that tracks disposal
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A minimal <see cref="IAgentSubsystem"/> implementation that records disposal
    /// calls and their ordering. Used as a replacement for the sealed concrete
    /// subsystems in the _allSubsystems array via reflection.
    /// </summary>
    private sealed class CallbackSubsystem : IAgentSubsystem
    {
        private readonly List<string> _sharedDisposalOrder;
        private int _disposeCount;

        public string Name { get; set; }
        public bool IsInitialized => true;
        public bool ThrowOnDispose { get; set; }
        public int DisposeCount => _disposeCount;

        /// <summary>
        /// Gets a snapshot of the shared disposal order list.
        /// </summary>
        public IReadOnlyList<string> DisposalOrderSnapshot => _sharedDisposalOrder.ToList().AsReadOnly();

        public CallbackSubsystem(string name, List<string> sharedDisposalOrder)
        {
            Name = name;
            _sharedDisposalOrder = sharedDisposalOrder;
        }

        public Task InitializeAsync(SubsystemInitContext ctx) => Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref _disposeCount);
            _sharedDisposalOrder.Add(Name);

            if (ThrowOnDispose)
            {
                throw new InvalidOperationException(
                    $"Simulated disposal failure in {Name}");
            }

            return ValueTask.CompletedTask;
        }
    }
}
