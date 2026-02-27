// <copyright file="MousePositionTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

using Ouroboros.Core.Monads;

public static partial class PerceptionTools
{
    /// <summary>
    /// Get current mouse position.
    /// </summary>
    public class MousePositionTool : ITool
    {
        public string Name => "get_mouse_position";
        public string Description => "Get the current mouse cursor position on screen.";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            await Task.CompletedTask;
            try
            {
                GetCursorPos(out POINT point);
                return Result<string, string>.Success($"🖱️ Mouse position: ({point.X}, {point.Y})");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to get mouse position: {ex.Message}");
            }
        }
    }
}
