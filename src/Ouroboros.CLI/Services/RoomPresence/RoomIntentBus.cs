// Copyright (c) Ouroboros. All rights reserved.
namespace Ouroboros.CLI.Services.RoomPresence;

/// <summary>
/// Static event bus for cross-mode communication between RoomMode and ImmersiveMode.
///
/// RoomMode runs as a background task while ImmersiveMode owns the foreground console.
/// This bus lets room events (Iaret speaking, user addressing Iaret directly) surface
/// in the ImmersiveMode chat display without shared mutable state or direct coupling.
/// </summary>
public static class RoomIntentBus
{
    /// <summary>
    /// Fired when Iaret interjects in the room.
    /// Parameters: (personaName, speech text).
    /// ImmersiveMode subscribes to display the interjection in the chat pane.
    /// </summary>
    public static event Action<string, string>? OnIaretInterjected;

    /// <summary>
    /// Fired when someone in the room addresses Iaret directly by name.
    /// Parameters: (speakerName, utterance text).
    /// ImmersiveMode can use this to inject the utterance into the chat loop,
    /// or RoomMode generates a guaranteed direct response.
    /// </summary>
    public static event Action<string, string>? OnUserAddressedIaret;

    /// <summary>
    /// Fired when a speaker is identified (or re-identified) by voice signature.
    /// Parameters: (speakerLabel, isOwner).
    /// ImmersiveMode can use this to display who is speaking.
    /// </summary>
    public static event Action<string, bool>? OnSpeakerIdentified;

    internal static void FireInterjection(string personaName, string speech)
        => OnIaretInterjected?.Invoke(personaName, speech);

    internal static void FireAddressedIaret(string speaker, string utterance)
        => OnUserAddressedIaret?.Invoke(speaker, utterance);

    internal static void FireSpeakerIdentified(string speakerLabel, bool isOwner)
        => OnSpeakerIdentified?.Invoke(speakerLabel, isOwner);

    /// <summary>Removes all subscribers (call on session teardown).</summary>
    public static void Reset()
    {
        OnIaretInterjected   = null;
        OnUserAddressedIaret = null;
        OnSpeakerIdentified  = null;
    }
}
