namespace Ouroboros.Application.Embodied;

/// <summary>
/// Represents a detected object in visual observation.
/// </summary>
/// <param name="Label">Object class label</param>
/// <param name="Confidence">Detection confidence score (0-1)</param>
/// <param name="BoundingBox">Bounding box coordinates</param>
public sealed record DetectedObject(
    string Label,
    float Confidence,
    BoundingBox BoundingBox);