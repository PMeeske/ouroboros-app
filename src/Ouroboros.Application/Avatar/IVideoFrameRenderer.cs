// <copyright file="IVideoFrameRenderer.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Avatar;

/// <summary>
/// Extended renderer interface for renderers that support binary video frame broadcasting.
/// Implement this alongside <see cref="IAvatarRenderer"/> to receive
/// SD-generated frames from <see cref="AvatarVideoStream"/>.
/// </summary>
public interface IVideoFrameRenderer
{
    /// <summary>
    /// Broadcasts a raw JPEG video frame to all connected viewers.
    /// </summary>
    /// <param name="jpegBytes">JPEG-encoded frame bytes.</param>
    Task BroadcastFrameAsync(byte[] jpegBytes);
}
