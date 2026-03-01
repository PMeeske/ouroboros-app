using FluentAssertions;
using Ouroboros.Application.Services;
using Xunit;

namespace Ouroboros.Tests.Services;

[Trait("Category", "Unit")]
public class ServicesModels2Tests
{
    // --- AccessPattern ---

    [Fact]
    public void AccessPattern_ShouldHaveDefaults()
    {
        var pattern = new AccessPattern();

        pattern.PointId.Should().BeEmpty();
        pattern.FilePath.Should().BeEmpty();
        pattern.AccessCount.Should().Be(0);
        pattern.CoAccessedWith.Should().BeEmpty();
    }

    [Fact]
    public void AccessPattern_ShouldSetProperties()
    {
        var pattern = new AccessPattern
        {
            PointId = "p1",
            FilePath = "/src/test.cs",
            AccessCount = 5,
            CoAccessedWith = new List<string> { "p2", "p3" }
        };

        pattern.PointId.Should().Be("p1");
        pattern.AccessCount.Should().Be(5);
        pattern.CoAccessedWith.Should().HaveCount(2);
    }

    // --- ActionSuggestionResult ---

    [Fact]
    public void ActionSuggestionResult_ShouldHaveDefaults()
    {
        var result = new ActionSuggestionResult();

        result.Success.Should().BeFalse();
        result.Suggestion.Should().BeEmpty();
        result.ErrorMessage.Should().BeNull();
        result.Goal.Should().BeNull();
    }

    // --- AntiHallucinationStats ---

    [Fact]
    public void AntiHallucinationStats_ShouldHaveDefaults()
    {
        var stats = new AntiHallucinationStats();

        stats.HallucinationCount.Should().Be(0);
        stats.VerifiedActionCount.Should().Be(0);
        stats.PendingVerifications.Should().Be(0);
        stats.RecentVerifications.Should().BeEmpty();
        stats.HallucinationRate.Should().Be(0.0);
    }

    // --- AutonomousConfig ---

    [Fact]
    public void AutonomousConfig_ShouldHaveDefaults()
    {
        var config = new AutonomousConfig();

        config.ThinkingIntervalSeconds.Should().Be(30);
        config.CuriosityIntervalSeconds.Should().Be(120);
        config.ActionIntervalSeconds.Should().Be(10);
        config.PersistenceIntervalSeconds.Should().Be(60);
        config.ReorganizationCycleInterval.Should().Be(10);
        config.MinReorganizationIntervalMinutes.Should().Be(5);
        config.ShareDiscoveryProbability.Should().Be(0.3);
        config.ReportActions.Should().BeTrue();
        config.AllowedAutonomousTools.Should().NotBeEmpty();
    }

    [Fact]
    public void AutonomousConfig_AllowedTools_ShouldContainExpectedTools()
    {
        var config = new AutonomousConfig();

        config.AllowedAutonomousTools.Should().Contain("capture_screen");
        config.AllowedAutonomousTools.Should().Contain("search_my_code");
        config.AllowedAutonomousTools.Should().Contain("modify_my_code");
    }

    // --- ClaimVerification ---

    [Fact]
    public void ClaimVerification_ShouldSetProperties()
    {
        var verification = new ClaimVerification
        {
            IsValid = true,
            Reason = "File exists",
            ClaimType = "file_existence"
        };

        verification.IsValid.Should().BeTrue();
        verification.Reason.Should().Be("File exists");
        verification.ClaimType.Should().Be("file_existence");
    }

    // --- ConversationSession ---

    [Fact]
    public void ConversationSession_ShouldSetProperties()
    {
        var now = DateTime.UtcNow;
        var turns = new List<Ouroboros.Application.Services.ConversationTurn>();
        var session = new ConversationSession("s1", "Ouroboros", now, now, turns, "Summary");

        session.SessionId.Should().Be("s1");
        session.PersonaName.Should().Be("Ouroboros");
        session.Summary.Should().Be("Summary");
    }

    [Fact]
    public void ConversationSession_Summary_ShouldBeNullByDefault()
    {
        var session = new ConversationSession("s1", "Ouro", DateTime.UtcNow, DateTime.UtcNow, new List<Ouroboros.Application.Services.ConversationTurn>());

        session.Summary.Should().BeNull();
    }

    // --- ConversationStats ---

    [Fact]
    public void ConversationStats_ShouldSetProperties()
    {
        var stats = new ConversationStats
        {
            TotalSessions = 10,
            TotalTurns = 100,
            CurrentSessionTurns = 5,
            OldestMemory = DateTime.UtcNow.AddDays(-7)
        };

        stats.TotalSessions.Should().Be(10);
        stats.TotalTurns.Should().Be(100);
        stats.OldestMemory.Should().NotBeNull();
    }

    // --- DetectedElement ---

    [Fact]
    public void DetectedElement_ShouldHaveDefaults()
    {
        var element = new DetectedElement();

        element.Type.Should().BeEmpty();
        element.Label.Should().BeEmpty();
        element.Position.Should().BeNull();
        element.State.Should().BeNull();
    }

    // --- IndexStats ---

    [Fact]
    public void IndexStats_ShouldSetProperties()
    {
        var stats = new IndexStats
        {
            TotalVectors = 1000,
            IndexedFiles = 50,
            CollectionName = "code",
            VectorSize = 384
        };

        stats.TotalVectors.Should().Be(1000);
        stats.IndexedFiles.Should().Be(50);
        stats.CollectionName.Should().Be("code");
        stats.VectorSize.Should().Be(384);
    }

    // --- IndexingProgress ---

    [Fact]
    public void IndexingProgress_ShouldSetProperties()
    {
        var progress = new IndexingProgress
        {
            TotalFiles = 100,
            ProcessedFiles = 50,
            IndexedChunks = 200,
            SkippedFiles = 5,
            ErrorFiles = 2,
            CurrentFile = "test.cs",
            IsComplete = false
        };

        progress.TotalFiles.Should().Be(100);
        progress.ProcessedFiles.Should().Be(50);
        progress.IndexedChunks.Should().Be(200);
        progress.SkippedFiles.Should().Be(5);
        progress.ErrorFiles.Should().Be(2);
        progress.IsComplete.Should().BeFalse();
    }

    // --- MindStateSnapshot ---

    [Fact]
    public void MindStateSnapshot_ShouldHaveDefaults()
    {
        var snapshot = new MindStateSnapshot();

        snapshot.PersonaName.Should().Be("Ouroboros");
        snapshot.ThoughtCount.Should().Be(0);
        snapshot.LearnedFacts.Should().BeEmpty();
        snapshot.Interests.Should().BeEmpty();
        snapshot.RecentThoughts.Should().BeEmpty();
    }

    [Fact]
    public void MindStateSnapshot_ToSummaryText_ShouldContainPersonaName()
    {
        var snapshot = new MindStateSnapshot { PersonaName = "TestAI" };

        var summary = snapshot.ToSummaryText();

        summary.Should().Contain("TestAI");
    }

    [Fact]
    public void MindStateSnapshot_ToSummaryText_WithInterests_ShouldListThem()
    {
        var snapshot = new MindStateSnapshot
        {
            Interests = new List<string> { "AI", "Coding" }
        };

        var summary = snapshot.ToSummaryText();

        summary.Should().Contain("AI");
        summary.Should().Contain("Coding");
    }

    [Fact]
    public void MindStateSnapshot_ToSummaryText_WithFacts_ShouldListThem()
    {
        var snapshot = new MindStateSnapshot
        {
            LearnedFacts = new List<string> { "Fact one", "Fact two" }
        };

        var summary = snapshot.ToSummaryText();

        summary.Should().Contain("Fact one");
    }

    // --- ModificationVerification ---

    [Fact]
    public void ModificationVerification_ShouldSetProperties()
    {
        var verification = new ModificationVerification
        {
            FilePath = "/src/test.cs",
            FileExisted = true,
            BeforeHash = "abc",
            AfterHash = "def",
            WasVerified = true,
            WasModified = true
        };

        verification.FilePath.Should().Be("/src/test.cs");
        verification.WasVerified.Should().BeTrue();
        verification.WasModified.Should().BeTrue();
    }

    // --- ThoughtAtom ---

    [Fact]
    public void ThoughtAtom_ShouldSetProperties()
    {
        var atom = new ThoughtAtom
        {
            StreamId = "stream-1",
            Content = "Analyzing pattern",
            Type = ThoughtAtomType.Derivation,
            SequenceNumber = 3
        };

        atom.StreamId.Should().Be("stream-1");
        atom.Content.Should().Be("Analyzing pattern");
        atom.Type.Should().Be(ThoughtAtomType.Derivation);
        atom.SequenceNumber.Should().Be(3);
    }

    // --- ThoughtAtomType ---

    [Fact]
    public void ThoughtAtomType_ShouldHave6Values()
    {
        Enum.GetValues<ThoughtAtomType>().Should().HaveCount(6);
    }

    // --- PresenceConfig ---

    [Fact]
    public void PresenceConfig_ShouldHaveDefaults()
    {
        var config = new PresenceConfig();

        config.CheckIntervalSeconds.Should().Be(5);
        config.PresenceThreshold.Should().Be(0.6);
        config.PresenceConfirmationFrames.Should().Be(2);
        config.AbsenceConfirmationFrames.Should().Be(6);
        config.UseWifi.Should().BeTrue();
        config.UseCamera.Should().BeFalse();
        config.UseInputActivity.Should().BeTrue();
        config.InputIdleThresholdSeconds.Should().Be(300);
    }

    // --- PresenceCheckResult ---

    [Fact]
    public void PresenceCheckResult_ShouldSetProperties()
    {
        var result = new PresenceCheckResult
        {
            IsPresent = true,
            OverallConfidence = 0.9,
            WifiDevicesNearby = 3,
            RecentInputActivity = true
        };

        result.IsPresent.Should().BeTrue();
        result.OverallConfidence.Should().Be(0.9);
        result.WifiDevicesNearby.Should().Be(3);
    }

    // --- PresenceEvent ---

    [Fact]
    public void PresenceEvent_ShouldSetProperties()
    {
        var evt = new PresenceEvent
        {
            State = PresenceState.Present,
            Timestamp = DateTime.UtcNow,
            Confidence = 0.95,
            Source = "wifi"
        };

        evt.State.Should().Be(PresenceState.Present);
        evt.Confidence.Should().Be(0.95);
        evt.Source.Should().Be("wifi");
    }

    // --- VisionBackend ---

    [Fact]
    public void VisionBackend_ShouldHave2Values()
    {
        Enum.GetValues<VisionBackend>().Should().HaveCount(2);
    }

    // --- VisionConfig ---

    [Fact]
    public void VisionConfig_ShouldHaveDefaults()
    {
        var config = new VisionConfig();

        config.Backend.Should().Be(VisionBackend.Ollama);
        config.OllamaEndpoint.Should().Be("http://localhost:11434");
        config.OllamaVisionModel.Should().Be("llava:latest");
        config.OpenAIApiKey.Should().BeNull();
    }

    // --- VisionResult ---

    [Fact]
    public void VisionResult_Failure_ShouldSetError()
    {
        var result = VisionResult.Failure("No image found");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("No image found");
    }

    // --- ScreenshotDetectionResult ---

    [Fact]
    public void ScreenshotDetectionResult_ShouldHaveDefaults()
    {
        var result = new ScreenshotDetectionResult();

        result.Success.Should().BeFalse();
        result.Elements.Should().BeEmpty();
    }

    // --- ScreenshotDiffResult ---

    [Fact]
    public void ScreenshotDiffResult_ShouldSetProperties()
    {
        var result = new ScreenshotDiffResult
        {
            Success = true,
            BeforeDescription = "Login page",
            AfterDescription = "Dashboard",
            Changes = "Navigation changed"
        };

        result.Success.Should().BeTrue();
        result.Changes.Should().Be("Navigation changed");
    }

    // --- TextExtractionResult ---

    [Fact]
    public void TextExtractionResult_ShouldSetProperties()
    {
        var result = new TextExtractionResult
        {
            Success = true,
            ExtractedText = "Hello World"
        };

        result.Success.Should().BeTrue();
        result.ExtractedText.Should().Be("Hello World");
    }

    // --- WarmupResult ---

    [Fact]
    public void WarmupResult_ShouldHaveDefaults()
    {
        var result = new WarmupResult();

        result.Success.Should().BeFalse();
        result.Steps.Should().BeEmpty();
        result.SeedThoughts.Should().BeEmpty();
        result.ToolWarmupResults.Should().BeEmpty();
    }

    [Fact]
    public void WarmupResult_Summary_Success_ShouldContainDetails()
    {
        var result = new WarmupResult
        {
            Success = true,
            Duration = TimeSpan.FromSeconds(5),
            ThinkingReady = true,
            SearchReady = true,
            ToolsSuccessCount = 3,
            ToolsTestedCount = 4,
            SelfIndexReady = true,
            SelfAwarenessReady = true
        };

        result.Summary.Should().Contain("complete");
        result.Summary.Should().Contain("Thinking=True");
    }

    [Fact]
    public void WarmupResult_Summary_Failure_ShouldContainError()
    {
        var result = new WarmupResult
        {
            Success = false,
            Error = "Timeout"
        };

        result.Summary.Should().Contain("failed");
        result.Summary.Should().Contain("Timeout");
    }
}
