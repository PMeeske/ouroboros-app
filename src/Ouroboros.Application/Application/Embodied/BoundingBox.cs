namespace Ouroboros.Application.Embodied;

/// <summary>
/// Represents a bounding box for object detection.
/// </summary>
/// <param name="X">X coordinate of top-left corner</param>
/// <param name="Y">Y coordinate of top-left corner</param>
/// <param name="Width">Width of bounding box</param>
/// <param name="Height">Height of bounding box</param>
public sealed record BoundingBox(
    int X,
    int Y,
    int Width,
    int Height);