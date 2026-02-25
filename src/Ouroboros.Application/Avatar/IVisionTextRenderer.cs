// <copyright file="IVisionTextRenderer.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Avatar;

/// <summary>
/// Extended renderer interface for renderers that support streaming vision text output.
/// Implement this alongside <see cref="IAvatarRenderer"/> to receive
/// streaming tokens from the Ollama vision model live analysis.
/// </summary>
public interface IVisionTextRenderer
{
    /// <summary>
    /// Broadcasts a streaming vision text token to all connected viewers.
    /// </summary>
    /// <param name="text">Text token or chunk from the vision model.</param>
    /// <param name="isNewFrame">If true, signals the start of a new frame analysis (clears previous text).</param>
    Task BroadcastVisionTextAsync(string text, bool isNewFrame = false);
}
