// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Commands;

using Ouroboros.CLI.Services.RoomPresence;

public sealed partial class RoomMode
{
    /// <summary>
    /// Initializes the best available STT backend for room listening.
    /// Internal so <see cref="ImmersiveMode"/> can call it for <c>--room-mode</c>.
    /// Delegates to <see cref="Services.SharedAgentBootstrap.CreateSttService"/>.
    /// </summary>
    internal static Task<Ouroboros.Providers.SpeechToText.ISpeechToTextService?> InitializeSttForRoomAsync()
        => Services.SharedAgentBootstrap.CreateSttService(
            log: msg => Console.WriteLine($"  [OK] STT: {msg}"));

    // STT initialization, causal extraction, and graph building consolidated in SharedAgentBootstrap.
}

/// <summary>Extension helpers on DetectedPerson used only by RoomMode.</summary>
internal static class DetectedPersonExtensions
{
    public static bool IsNewPerson(this DetectedPerson p) => p.InteractionCount <= 1;
}
