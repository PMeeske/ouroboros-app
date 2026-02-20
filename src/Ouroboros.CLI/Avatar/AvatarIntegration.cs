// <copyright file="AvatarIntegration.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Reactive.Linq;
using Ouroboros.Application.Avatar;
using Ouroboros.Core.EmbodiedInteraction;

namespace Ouroboros.CLI.Avatar;

/// <summary>
/// Wires the interactive avatar into the existing CLI voice/immersive mode.
/// This is the single integration point — call <see cref="CreateAndStartAsync"/>
/// when the CLI launches with the <c>--avatar</c> flag.
/// </summary>
public static class AvatarIntegration
{
    /// <summary>
    /// Creates, configures, and starts the avatar system with the web renderer.
    /// When <paramref name="visionModel"/> or <paramref name="virtualSelf"/> is provided,
    /// also starts the live AI-generated video stream via Stable Diffusion.
    /// </summary>
    /// <param name="personaName">Active persona name (e.g. "Iaret").</param>
    /// <param name="port">WebSocket port for the avatar viewer (0 = auto-assign from default).</param>
    /// <param name="assetDirectory">Optional override for the avatar asset directory.</param>
    /// <param name="visionModel">Optional vision model for the video stream perception loop.</param>
    /// <param name="virtualSelf">Optional VirtualSelf for closed-loop perception publishing.</param>
    /// <param name="ollamaEndpoint">Ollama server endpoint for SD generation.</param>
    /// <param name="sdModel">Stable Diffusion model name for generation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The running avatar service and optional video stream (caller must dispose both).</returns>
    public static async Task<(InteractiveAvatarService Service, AvatarVideoStream? VideoStream)> CreateAndStartAsync(
        string personaName = "Iaret",
        int port = 0,
        string? assetDirectory = null,
        IVisionModel? visionModel = null,
        VirtualSelf? virtualSelf = null,
        string ollamaEndpoint = "http://localhost:11434",
        string sdModel = "stable-diffusion",
        CancellationToken ct = default)
    {
        var service = new InteractiveAvatarService(personaName);
        var renderer = new WebAvatarRenderer(port, assetDirectory);
        service.AttachRenderer(renderer);
        await service.StartAsync(ct);

        var generator = new AvatarVideoGenerator(ollamaEndpoint, sdModel, logger: null);
        var videoStream = new AvatarVideoStream(generator, service, visionModel, virtualSelf, assetDirectory);
        _ = videoStream.StartAsync(ct); // fire and forget — runs in background

        return (service, videoStream);
    }

    /// <summary>
    /// Binds the avatar to presence state changes expressed as string names.
    /// Use this when the <c>AgentPresenceState</c> enum isn't directly accessible.
    /// </summary>
    /// <param name="avatar">The running avatar service.</param>
    /// <param name="presenceChanges">Observable of presence state name strings.</param>
    /// <param name="moodProvider">Returns the current mood name.</param>
    public static void BindToPresenceStream(
        InteractiveAvatarService avatar,
        IObservable<string> presenceChanges,
        Func<string> moodProvider)
    {
        avatar.BindPresence(
            presenceChanges,
            moodProvider,
            energyProvider: () => 0.5,
            positivityProvider: () => 0.5);
    }

    /// <summary>
    /// Updates the avatar directly from a presence state name and mood.
    /// Use in code paths where events aren't Rx-observable.
    /// </summary>
    public static void PushState(
        InteractiveAvatarService avatar,
        string presenceState,
        string mood,
        double energy = 0.5,
        double positivity = 0.5)
    {
        avatar.SetPresenceState(presenceState, mood, energy, positivity);
    }
}
