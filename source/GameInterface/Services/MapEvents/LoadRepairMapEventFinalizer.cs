using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Registry.Auto;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents;

/// <summary>
/// [Server] Finalizes a map event and publishes its end when the FinalizeEventAux postfix will not. That postfix
/// publishes only while the sync policy blocks originals, and the load repairs run before ServerRunningState, when
/// it still allows them. Without this the event and its graph keep their ids, and the MapEventFinalized listeners
/// never run (an offline player's party, whose parking waited for the battle to end, stays active).
/// The end is announced whether or not the event is registered: the cleaner also finalizes an unlisted event a saved
/// player party points at, and MapEventRegistry registers only the events the manager lists.
/// </summary>
internal static class LoadRepairMapEventFinalizer
{
    public static void FinalizeEvent(MapEvent mapEvent, IMessageBroker messageBroker, IObjectManager objectManager)
    {
        var postfixIsSilent = ModInformation.IsServer && CallOriginalPolicy.IsOriginalAllowed();
        var wasRegistered = objectManager.Contains(mapEvent);

        mapEvent.FinalizeEvent();

        if (!postfixIsSilent || !mapEvent.IsFinalized)
            return;

        // A finalize that threw past WaitingRemoval was already published and unregistered by ReleaseFailedFinalization.
        // For an unregistered event that release cannot be told apart, so its end is announced once more, which every
        // MapEventFinalized listener tolerates.
        if (wasRegistered && !objectManager.Contains(mapEvent))
            return;

        // The same order as the postfix: listeners still resolve the event before its registry entry goes.
        messageBroker.Publish(mapEvent, new MapEventFinalized(mapEvent));
        if (objectManager.Contains(mapEvent))
            messageBroker.Publish(mapEvent, new InstanceDestroyed<MapEvent>(mapEvent));
    }
}
