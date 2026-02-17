// <copyright file="IAvatarRenderer.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Avatar;

/// <summary>
/// Abstraction for rendering the avatar in any target (terminal, web, GUI).
/// </summary>
public interface IAvatarRenderer : IAsyncDisposable
{
    /// <summary>Gets whether the renderer is currently active.</summary>
    bool IsActive { get; }

    /// <summary>Starts the renderer (opens window, launches server, etc.).</summary>
    Task StartAsync(CancellationToken ct = default);

    /// <summary>Pushes a new state snapshot to the renderer.</summary>
    Task UpdateStateAsync(AvatarStateSnapshot state, CancellationToken ct = default);

    /// <summary>Stops the renderer gracefully.</summary>
    Task StopAsync(CancellationToken ct = default);
}
