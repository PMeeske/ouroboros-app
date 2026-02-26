// <copyright file="DetectionEvent.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Ouroboros.Application.Services;

/// <summary>
/// A detection event produced by a micro detection module.
/// Represents a single observation from presence, motion, gesture, or speech detection.
/// </summary>
public sealed record DetectionEvent(
    /// <summary>Name of the module that produced this event.</summary>
    string ModuleName,

    /// <summary>Type of event: "presence", "absence", "motion", "gesture", "speech", etc.</summary>
    string EventType,

    /// <summary>Confidence of the detection, from 0.0 (no confidence) to 1.0 (certain).</summary>
    double Confidence,

    /// <summary>UTC timestamp of the detection.</summary>
    DateTime Timestamp,

    /// <summary>Optional structured payload with module-specific data.</summary>
    JsonElement? Payload = null);
