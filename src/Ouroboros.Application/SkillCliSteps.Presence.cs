#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Skill CLI Steps - Presence Detection
// ==========================================================

using System.Text;
using Ouroboros.Agent.MetaAI;

namespace Ouroboros.Application;

public static partial class SkillCliSteps
{
    #region Presence Detection CLI Steps

    /// <summary>
    /// Shared presence detector for CLI access.
    /// </summary>
    public static Services.PresenceDetector? SharedPresenceDetector { get; set; }

    /// <summary>
    /// Check user presence status.
    /// Reports whether the user is detected as present via input/WiFi/camera.
    /// Usage: CheckPresence | UseOutput
    /// </summary>
    [PipelineToken("CheckPresence", "PresenceStatus", "IsUserHere", "WhereAmI")]
    public static Step<CliPipelineState, CliPipelineState> CheckPresence(string? _ = null)
        => async s =>
        {
            if (SharedPresenceDetector == null)
            {
                s.Output = "⚠️ Presence detection not initialized.";
                Console.WriteLine(s.Output);
                return s;
            }

            var result = await SharedPresenceDetector.CheckPresenceAsync();

            var sb = new StringBuilder();
            sb.AppendLine("👁️ **Presence Detection Status**");
            sb.AppendLine($"   State: {(result.IsPresent ? "✅ User Present" : "❌ User Absent")}");
            sb.AppendLine($"   Confidence: {result.OverallConfidence:P0}");
            sb.AppendLine();
            sb.AppendLine("📊 **Detection Sources:**");
            sb.AppendLine($"   💻 Input Activity: {(result.RecentInputActivity ? "✓" : "○")} ({result.InputActivityConfidence:P0})");
            sb.AppendLine($"   📶 WiFi/Network: {result.WifiDevicesNearby} devices ({result.WifiPresenceConfidence:P0})");
            sb.AppendLine($"   📷 Camera/Motion: {(result.MotionDetected ? "✓" : "○")} ({result.CameraConfidence:P0})");
            sb.AppendLine();
            sb.AppendLine($"   Last check: {result.Timestamp:HH:mm:ss}");

            Console.WriteLine(sb.ToString());
            s.Output = sb.ToString();
            return s;
        };

    /// <summary>
    /// Get presence detector configuration and state.
    /// Usage: PresenceConfig | UseOutput
    /// </summary>
    [PipelineToken("PresenceConfig", "PresenceSettings")]
    public static Step<CliPipelineState, CliPipelineState> PresenceConfig(string? _ = null)
        => async s =>
        {
            if (SharedPresenceDetector == null)
            {
                s.Output = "⚠️ Presence detection not initialized.";
                Console.WriteLine(s.Output);
                return s;
            }

            var sb = new StringBuilder();
            sb.AppendLine("⚙️ **Presence Detection Configuration**");
            sb.AppendLine($"   Monitoring: {(SharedPresenceDetector.IsMonitoring ? "✅ Active" : "❌ Stopped")}");
            sb.AppendLine($"   Current State: {SharedPresenceDetector.CurrentState}");
            sb.AppendLine($"   Last Presence: {SharedPresenceDetector.LastPresenceTime:yyyy-MM-dd HH:mm:ss}");

            Console.WriteLine(sb.ToString());
            s.Output = sb.ToString();
            await Task.CompletedTask;
            return s;
        };

    #endregion
}
