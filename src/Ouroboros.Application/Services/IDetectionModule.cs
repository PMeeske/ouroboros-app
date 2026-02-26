// <copyright file="IDetectionModule.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Services;

/// <summary>
/// A pluggable detection module that runs inside the <see cref="MicroDetectionWorker"/>.
/// Each module encapsulates a single detection capability (presence, motion, speech, etc.)
/// and is polled at its configured interval by the worker.
/// </summary>
public interface IDetectionModule : IDisposable
{
    /// <summary>Unique name for this detection module (e.g., "presence", "gesture").</summary>
    string Name { get; }

    /// <summary>How often this module should be polled for detection.</summary>
    TimeSpan Interval { get; }

    /// <summary>
    /// Returns true if enough time has elapsed since the last detection for this module
    /// to be polled again.
    /// </summary>
    bool IsReady();

    /// <summary>
    /// Performs a single detection pass. Returns a <see cref="DetectionEvent"/> if something
    /// was detected, or null if nothing noteworthy occurred.
    /// </summary>
    Task<DetectionEvent?> DetectAsync(CancellationToken ct);
}
