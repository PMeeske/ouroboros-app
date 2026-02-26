using Ouroboros.CLI.Services.RoomPresence;

namespace Ouroboros.Tests.CLI.Services.RoomPresence;

[Trait("Category", "Unit")]
public class VoiceSignatureServiceTests
{
    private static VoiceSignature MakeSig(double rms = 0.5, double zcr = 2000, double rate = 3.0, double dyn = 0.5, double dur = 5.0)
        => new(rms, zcr, rate, dyn, dur);

    [Fact]
    public void HasOwner_InitiallyFalse()
    {
        var svc = new VoiceSignatureService();

        svc.HasOwner.Should().BeFalse();
    }

    [Fact]
    public void TryMatch_EmptyProfiles_ReturnsNull()
    {
        var svc = new VoiceSignatureService();

        var result = svc.TryMatch(MakeSig());

        result.Should().BeNull();
    }

    [Fact]
    public void AddSample_CreatesProfile()
    {
        var svc = new VoiceSignatureService();
        var sig = MakeSig();

        var label = svc.AddSample("speaker-1", sig);

        label.Should().Be("speaker-1");
    }

    [Fact]
    public void EnrollOwner_SetsOwner()
    {
        var svc = new VoiceSignatureService();
        var sig = MakeSig();

        svc.EnrollOwner("speaker-1", sig);

        svc.HasOwner.Should().BeTrue();
        svc.IsOwner("speaker-1").Should().BeTrue();
        svc.IsOwner("speaker-2").Should().BeFalse();
    }

    [Fact]
    public void AddSample_ForOwner_ReturnsUserLabel()
    {
        var svc = new VoiceSignatureService();
        var sig = MakeSig();

        svc.EnrollOwner("speaker-1", sig);
        var label = svc.AddSample("speaker-1", MakeSig(0.51, 2010, 3.1, 0.51, 5.0));

        label.Should().Be("User");
    }

    [Fact]
    public void TryMatch_WithIdenticalSig_ReturnsMatch()
    {
        var svc = new VoiceSignatureService();
        var sig = MakeSig(0.5, 2000, 3.0, 0.5, 5.0);

        svc.AddSample("speaker-1", sig);

        var match = svc.TryMatch(sig);

        match.Should().NotBeNull();
        match!.Value.SpeakerId.Should().Be("speaker-1");
    }

    [Fact]
    public void TryMatch_WithVeryDifferentSig_ReturnsNull()
    {
        var svc = new VoiceSignatureService();

        svc.AddSample("speaker-1", MakeSig(0.1, 500, 1.0, 0.1, 3.0));

        var match = svc.TryMatch(MakeSig(0.9, 3500, 5.0, 0.9, 10.0));

        // Very different signatures should not match
        match.Should().BeNull();
    }

    [Theory]
    [InlineData("remember my voice", "Iaret", true)]
    [InlineData("enroll my voice", "Iaret", true)]
    [InlineData("that's me", "Iaret", true)]
    [InlineData("that is me", "Iaret", true)]
    [InlineData("Iaret, recognise me", "Iaret", true)]
    [InlineData("Iaret, recognize me", "Iaret", true)]
    [InlineData("hello there", "Iaret", false)]
    [InlineData("what time is it", "Iaret", false)]
    public void IsEnrollmentRequest_DetectsCorrectly(string text, string persona, bool expected)
    {
        VoiceSignatureService.IsEnrollmentRequest(text, persona).Should().Be(expected);
    }
}
