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
/// </summary>
internal static class LoadRepairMapEventFinalizer
{
    public static void FinalizeEvent(MapEvent mapEvent, IMessageBroker messageBroker, IObjectManager objectManager)
    {
        var postfixIsSilent = ModInformation.IsServer && CallOriginalPolicy.IsOriginalAllowed();

        mapEvent.FinalizeEvent();

        // A finalize that threw past WaitingRemoval was already published and unregistered by ReleaseFailedFinalization.
        if (!postfixIsSilent || !mapEvent.IsFinalized || !objectManager.Contains(mapEvent))
            return;

        // The same order as the postfix: listeners still resolve the event before its registry entry goes.
        messageBroker.Publish(mapEvent, new MapEventFinalized(mapEvent));
        messageBroker.Publish(mapEvent, new InstanceDestroyed<MapEvent>(mapEvent));
    }
}
