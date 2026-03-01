// <copyright file="ListCapturedImagesTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

using System.Text;
using Ouroboros.Core.Monads;

public static partial class PerceptionTools
{
    /// <summary>
    /// List captured images and screenshots.
    /// </summary>
    public class ListCapturedImagesTool : ITool
    {
        public string Name => "list_captured_images";
        public string Description => "List all captured screenshots and camera images. Shows recent captures with timestamps.";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            await Task.CompletedTask;
            try
            {
                if (!Directory.Exists(CaptureDirectory))
                {
                    return Result<string, string>.Success("No captures yet. Use `capture_screen` or `capture_camera` first.");
                }

                var files = Directory.GetFiles(CaptureDirectory)
                    .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".log"))
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .Take(20)
                    .ToList();

                if (files.Count == 0)
                {
                    return Result<string, string>.Success("No captures found.");
                }

                var sb = new StringBuilder();
                sb.AppendLine("📁 **Recent Captures**\n");

                foreach (var file in files)
                {
                    var info = new FileInfo(file);
                    var icon = file.EndsWith(".log") ? "📝" : "🖼️";
                    sb.AppendLine($"{icon} `{info.Name}` - {info.LastWriteTime:yyyy-MM-dd HH:mm} ({info.Length / 1024} KB)");
                }

                sb.AppendLine($"\n📂 Location: `{CaptureDirectory}`");

                return Result<string, string>.Success(sb.ToString());
            }
            catch (IOException ex)
            {
                return Result<string, string>.Failure($"Failed to list captures: {ex.Message}");
            }
        }
    }
}
