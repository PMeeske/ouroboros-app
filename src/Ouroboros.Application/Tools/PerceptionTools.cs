// <copyright file="PerceptionTools.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

using System.Runtime.InteropServices;
using System.Text;
using Ouroboros.Application.Services;

/// <summary>
/// Provides perception tools for Ouroboros - screen capture, camera, and active monitoring.
/// Enables proactive observation of user behavior.
///
/// Tool classes are in the Tools/Perception/ subdirectory:
///   ScreenCaptureTool, CameraCaptureTool, ActiveWindowTool, MousePositionTool,
///   WatchScreenTool, WatchUserActivityTool, AnalyzeImageTool, ListCapturedImagesTool,
///   SeeScreenTool, DescribeImageTool, ReadTextFromScreenTool, WhatAmIDoingTool, DetectObjectsTool.
/// </summary>
public static partial class PerceptionTools
{
    /// <summary>
    /// Directory to store captured screenshots and images.
    /// </summary>
    public static string CaptureDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".ouroboros", "captures");

    /// <summary>
    /// Event fired when screen content changes significantly.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CS0067:Event is never used", Justification = "Event is raised in conditional compilation (WatchScreenTool) and subscribed in ImmersiveMode.Skills")]
    public static event Action<string>? OnScreenChanged;

    /// <summary>
    /// Event fired when user activity is detected.
    /// </summary>
    public static event Action<string>? OnUserActivity;

    /// <summary>
    /// Shared vision service for AI-powered image understanding.
    /// </summary>
    public static VisionService? VisionService { get; set; }

    /// <summary>
    /// Creates all perception tools.
    /// </summary>
    public static IEnumerable<ITool> CreateAllTools()
    {
        yield return new ScreenCaptureTool();
        yield return new CameraCaptureTool();
        yield return new ActiveWindowTool();
        yield return new MousePositionTool();
        yield return new WatchScreenTool();
        yield return new WatchUserActivityTool();
        yield return new AnalyzeImageTool();
        yield return new ListCapturedImagesTool();

        // Vision AI tools
        yield return new SeeScreenTool();
        yield return new DescribeImageTool();
        yield return new ReadTextFromScreenTool();
        yield return new WhatAmIDoingTool();
        yield return new DetectObjectsTool();
    }

    #region Win32 APIs for screen/window access

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    #endregion
}
