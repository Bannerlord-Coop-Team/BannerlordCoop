using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.MapEvents.Initialization;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Handlers;

internal sealed class MapEventInitializationHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly IMapEventInitializationBarrier barrier;

    public MapEventInitializationHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        IMapEventInitializationBarrier barrier)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.barrier = barrier;
        messageBroker.Subscribe<NetworkMapEventPartyPending>(HandlePendingParty);
        messageBroker.Subscribe<NetworkMapEventInitialized>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkMapEventPartyPending>(HandlePendingParty);
        messageBroker.Unsubscribe<NetworkMapEventInitialized>(Handle);
    }

    private void HandlePendingParty(MessagePayload<NetworkMapEventPartyPending> payload)
    {
        var message = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MapEvent>(message.MapEventId, out var mapEvent)) return;
            if (!objectManager.TryGetObjectWithLogging<PartyBase>(message.PartyId, out var party)) return;

            if (message.IsCancellation)
                barrier.UnlockClientParty(mapEvent, party);
            else
                barrier.LockClientParty(mapEvent, party);
        }, context: nameof(NetworkMapEventPartyPending));
    }

    private void Handle(MessagePayload<NetworkMapEventInitialized> payload)
    {
        var message = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MapEvent>(message.MapEventId, out var mapEvent)) return;

            if (message.TroopUpgradeTrackerId != null)
            {
                if (!objectManager.TryGetObjectWithLogging<TroopUpgradeTracker>(
                        message.TroopUpgradeTrackerId,
                        out var tracker))
                {
                    barrier.CommitClient(mapEvent, isTerminal: true);
                    return;
                }

                using (new AllowedThread())
                {
                    mapEvent.TroopUpgradeTracker = tracker;
                }
            }

            barrier.CommitClient(mapEvent, message.IsTerminal);
        }, context: nameof(NetworkMapEventInitialized));
    }
}
