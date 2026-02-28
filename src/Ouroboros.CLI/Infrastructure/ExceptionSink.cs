// Copyright (c) Ouroboros. All rights reserved.

using Ouroboros.CLI.Mediator.Notifications;

namespace Ouroboros.CLI.Infrastructure;

/// <summary>
/// Global exception sink that routes exceptions into Iaret's consciousness.
/// Set <see cref="Current"/> during agent initialization so that fire-and-forget
/// tasks, global handlers, and any subsystem can publish exceptions without
/// needing a DI reference.
/// </summary>
public static class ExceptionSink
{
    private static IAgentEventSink? _sink;

    /// <summary>
    /// Sets the active sink.  Call once during agent initialization.
    /// </summary>
    public static void SetSink(IAgentEventSink sink) => _sink = sink;

    /// <summary>
    /// Clears the sink (call during shutdown to prevent leaks).
    /// </summary>
    public static void Clear() => _sink = null;

    /// <summary>
    /// Publishes an exception into Iaret's kernel event loop.
    /// Safe to call even when no sink is wired (silently drops).
    /// </summary>
    public static void Publish(Exception exception, string context, bool isFatal = false)
    {
        var sink = _sink;
        if (sink == null) return;

        try
        {
            sink.Enqueue(new ExceptionOccurredNotification(
                Context: context,
                ExceptionType: exception.GetType().Name,
                Message: exception.GetBaseException().Message,
                StackTrace: exception.StackTrace,
                IsFatal: isFatal));
        }
        catch
        {
            // Never throw from the exception sink itself.
            System.Diagnostics.Debug.WriteLine(
                $"[ExceptionSink] Failed to publish exception: {exception.Message}");
        }
    }
}
