using Common;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEvents.Messages;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.SiegeEvents.Handlers;

/// <summary>
/// Replicates the server activating or deactivating a siege's blockade.
/// </summary>
internal class SiegeBlockadeHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public SiegeBlockadeHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        messageBroker.Subscribe<SiegeBlockadeChanged>(Handle_SiegeBlockadeChanged);
        messageBroker.Subscribe<NetworkChangeSiegeBlockade>(Handle_NetworkChangeSiegeBlockade);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SiegeBlockadeChanged>(Handle_SiegeBlockadeChanged);
        messageBroker.Unsubscribe<NetworkChangeSiegeBlockade>(Handle_NetworkChangeSiegeBlockade);
    }

    private void Handle_SiegeBlockadeChanged(MessagePayload<SiegeBlockadeChanged> obj)
    {
        var siegeEvent = obj.What.SiegeEvent;

        if (!objectManager.TryGetId(siegeEvent, out var siegeEventId)) return;

        network.SendAll(new NetworkChangeSiegeBlockade(siegeEventId, siegeEvent.IsBlockadeActive));
    }

    private void Handle_NetworkChangeSiegeBlockade(MessagePayload<NetworkChangeSiegeBlockade> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            // Lifting the last blockading fleet can end the siege right after, so it may already be gone
            if (!objectManager.TryGetObject<SiegeEvent>(data.SiegeEventId, out var siegeEvent)) return;
            if (siegeEvent.IsBlockadeActive == data.IsBlockadeActive) return;

            // Run instead of set, so the blockading fleets are redrawn and the blockade events are raised
            using (new AllowedThread())
            {
                if (data.IsBlockadeActive) siegeEvent.ActivateBlockade();
                else siegeEvent.DeactivateBlockade();
            }
        }, context: nameof(SiegeBlockadeHandler));
    }
}
