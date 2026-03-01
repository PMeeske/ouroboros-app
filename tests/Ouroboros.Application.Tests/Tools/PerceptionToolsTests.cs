// Copyright (c) 2025 Ouroboros contributors. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ouroboros.Application.Tools;
using Xunit;

namespace Ouroboros.Tests.Tools;

[Trait("Category", "Unit")]
public class PerceptionToolsTests
{
    // ======================================================================
    // CreateAllTools
    // ======================================================================

    [Fact]
    public void CreateAllTools_ShouldReturnAllExpectedTools()
    {
        // Act
        var tools = PerceptionTools.CreateAllTools().ToList();

        // Assert
        tools.Should().NotBeEmpty();
        var names = tools.Select(t => t.Name).ToList();
        names.Should().Contain("capture_screen");
        names.Should().Contain("capture_camera");
        names.Should().Contain("get_active_window");
        names.Should().Contain("get_mouse_position");
        names.Should().Contain("watch_screen");
        names.Should().Contain("watch_user_activity");
        names.Should().Contain("analyze_image");
        names.Should().Contain("list_captured_images");
        names.Should().Contain("see_screen");
        names.Should().Contain("describe_image");
        names.Should().Contain("read_text_from_screen");
        names.Should().Contain("detect_objects");
    }

    [Fact]
    public void CreateAllTools_AllToolsShouldHaveNameAndDescription()
    {
        // Act
        var tools = PerceptionTools.CreateAllTools().ToList();

        // Assert
        tools.Should().AllSatisfy(t =>
        {
            t.Name.Should().NotBeNullOrWhiteSpace();
            t.Description.Should().NotBeNullOrWhiteSpace();
        });
    }

    // ======================================================================
    // ScreenCaptureTool — non-Windows returns failure
    // ======================================================================

#if !NET10_0_OR_GREATER_WINDOWS
    [Fact]
    public async Task ScreenCapture_OnNonWindows_ShouldReturnNotSupported()
    {
        // Arrange
        var tool = new PerceptionTools.ScreenCaptureTool();

        // Act
        var result = await tool.InvokeAsync("");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Contain("only supported on Windows");
    }
#endif

    // ======================================================================
    // AnalyzeImageTool
    // ======================================================================

    [Fact]
    public async Task AnalyzeImage_WithNonExistentFile_ShouldReturnFailure()
    {
        // Arrange
        var tool = new PerceptionTools.AnalyzeImageTool();

        // Act
        var result = await tool.InvokeAsync("/nonexistent_image_xyz_123.png");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Contain("not found");
    }

    [Fact]
    public async Task AnalyzeImage_WithVisionAnalyzer_ShouldUseAnalyzer()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        // Write a minimal valid BMP (1x1 pixel)
        await File.WriteAllBytesAsync(tempFile, CreateMinimalBmp());

        PerceptionTools.AnalyzeImageTool.VisionAnalyzer = (path, _) =>
            Task.FromResult($"I see an image at {Path.GetFileName(path)}");
        var tool = new PerceptionTools.AnalyzeImageTool();

        try
        {
            // Act
            var result = await tool.InvokeAsync(tempFile);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Contain("Image Analysis");
        }
        finally
        {
            PerceptionTools.AnalyzeImageTool.VisionAnalyzer = null;
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task AnalyzeImage_WithRelativePath_ShouldResolveFromCaptureDirectory()
    {
        // Arrange
        var tool = new PerceptionTools.AnalyzeImageTool();

        // Act — use relative path that doesn't exist
        var result = await tool.InvokeAsync("relative_image.png");

        // Assert — should try to resolve from CaptureDirectory and fail (not found)
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Contain("not found");
    }

    // ======================================================================
    // ListCapturedImagesTool
    // ======================================================================

    [Fact]
    public async Task ListCapturedImages_WhenNoCaptureDir_ShouldReturnNoCapturesMessage()
    {
        // Arrange
        var originalDir = PerceptionTools.CaptureDirectory;
        PerceptionTools.CaptureDirectory = Path.Combine(Path.GetTempPath(), $"ouro_no_captures_{Guid.NewGuid():N}");
        var tool = new PerceptionTools.ListCapturedImagesTool();

        try
        {
            // Act
            var result = await tool.InvokeAsync("");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Contain("No captures yet");
        }
        finally
        {
            PerceptionTools.CaptureDirectory = originalDir;
        }
    }

    [Fact]
    public async Task ListCapturedImages_WithImages_ShouldListThem()
    {
        // Arrange
        var originalDir = PerceptionTools.CaptureDirectory;
        var tempDir = Path.Combine(Path.GetTempPath(), $"ouro_captures_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        PerceptionTools.CaptureDirectory = tempDir;
        File.WriteAllText(Path.Combine(tempDir, "screen_20250101_120000.png"), "fake png");
        File.WriteAllText(Path.Combine(tempDir, "camera_20250101_120100.jpg"), "fake jpg");
        File.WriteAllText(Path.Combine(tempDir, "activity_20250101_120200.log"), "fake log");
        var tool = new PerceptionTools.ListCapturedImagesTool();

        try
        {
            // Act
            var result = await tool.InvokeAsync("");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Contain("Recent Captures");
            result.Value.Should().Contain("screen_20250101_120000.png");
            result.Value.Should().Contain("camera_20250101_120100.jpg");
            result.Value.Should().Contain("activity_20250101_120200.log");
        }
        finally
        {
            PerceptionTools.CaptureDirectory = originalDir;
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ListCapturedImages_ShouldIgnoreNonImageFiles()
    {
        // Arrange
        var originalDir = PerceptionTools.CaptureDirectory;
        var tempDir = Path.Combine(Path.GetTempPath(), $"ouro_captures2_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        PerceptionTools.CaptureDirectory = tempDir;
        File.WriteAllText(Path.Combine(tempDir, "data.csv"), "not an image");
        File.WriteAllText(Path.Combine(tempDir, "notes.txt"), "not an image");
        var tool = new PerceptionTools.ListCapturedImagesTool();

        try
        {
            // Act
            var result = await tool.InvokeAsync("");

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Contain("No captures found");
        }
        finally
        {
            PerceptionTools.CaptureDirectory = originalDir;
            Directory.Delete(tempDir, true);
        }
    }

    // ======================================================================
    // SeeScreenTool
    // ======================================================================

    [Fact]
    public async Task SeeScreen_WithNoVisionService_ShouldReturnFailure()
    {
        // Arrange
        PerceptionTools.VisionService = null;
        var tool = new PerceptionTools.SeeScreenTool();

        // Act
        var result = await tool.InvokeAsync("");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Contain("Vision service not available");
    }

    // ======================================================================
    // WatchScreenTool — platform-dependent, test non-Windows branch
    // ======================================================================

#if !NET10_0_OR_GREATER_WINDOWS
    [Fact]
    public async Task WatchScreen_OnNonWindows_ShouldReturnNotSupported()
    {
        // Arrange
        var tool = new PerceptionTools.WatchScreenTool();

        // Act
        var result = await tool.InvokeAsync("");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().Contain("only supported on Windows");
    }
#endif

    // ======================================================================
    // Helper
    // ======================================================================

    private static byte[] CreateMinimalBmp()
    {
        // Create a minimal 1x1 24-bit BMP
        var bmp = new byte[58];
        // BMP header
        bmp[0] = (byte)'B';
        bmp[1] = (byte)'M';
        // File size = 58 bytes
        bmp[2] = 58;
        // Pixel data offset = 54
        bmp[10] = 54;
        // DIB header size = 40
        bmp[14] = 40;
        // Width = 1
        bmp[18] = 1;
        // Height = 1
        bmp[22] = 1;
        // Planes = 1
        bmp[26] = 1;
        // Bits per pixel = 24
        bmp[28] = 24;
        // Pixel data (BGR) + padding
        bmp[54] = 0xFF;
        bmp[55] = 0xFF;
        bmp[56] = 0xFF;
        bmp[57] = 0x00; // padding
        return bmp;
    }
}
