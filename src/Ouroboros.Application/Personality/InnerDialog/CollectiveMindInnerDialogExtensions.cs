namespace Ouroboros.Application.Personality;

/// <summary>
/// Extension methods for integrating CollectiveMind with InnerDialogEngine.
/// </summary>
public static class CollectiveMindInnerDialogExtensions
{
    /// <summary>
    /// Creates a bridge connecting this CollectiveMind to an InnerDialogEngine.
    /// </summary>
    public static CollectiveMindBridge BridgeToInnerDialog(
        this CollectiveMind collectiveMind,
        InnerDialogEngine? innerDialogEngine = null)
    {
        return new CollectiveMindBridge(collectiveMind, innerDialogEngine);
    }

    /// <summary>
    /// Subscribes InnerDialogEngine to receive thoughts from CollectiveMind.
    /// </summary>
    public static IDisposable SubscribeToCollective(
        this InnerDialogEngine innerDialogEngine,
        CollectiveMind collectiveMind,
        Action<InnerThought>? onThought = null)
    {
        var bridge = new CollectiveMindBridge(collectiveMind, innerDialogEngine);

        return bridge.UnifiedThoughtStream.Subscribe(thought =>
        {
            onThought?.Invoke(thought);
        });
    }
}