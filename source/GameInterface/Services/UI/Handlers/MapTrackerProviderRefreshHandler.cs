using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.UI.Messages;
using GameInterface.Services.UI.Patches;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.UI.Handlers;

/// <summary>
/// Handler for resetting the trackers after the mainhero has been properly set up
/// </summary>
internal class MapTrackerProviderRefreshHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IMapTrackerProviderHolder holder;

    public MapTrackerProviderRefreshHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IMapTrackerProviderHolder holder)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.holder = holder;

        messageBroker.Subscribe<SwitchedPlayer>(Handle_SwitchedPlayer);

        messageBroker.Subscribe<MapTrackerPartyCreated>(Handle_MapTrackerPartyCreated);
        messageBroker.Subscribe<NetworkMapTrackerPartyCreated>(Handle_NetworkMapTrackerPartyCreated);

        messageBroker.Subscribe<MapTrackerPartyRemoved>(Handle_MapTrackerPartyRemoved);
        messageBroker.Subscribe<NetworkMapTrackerPartyRemoved>(Handle_NetworkMapTrackerPartyRemoved);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SwitchedPlayer>(Handle_SwitchedPlayer);

        messageBroker.Unsubscribe<MapTrackerPartyCreated>(Handle_MapTrackerPartyCreated);
        messageBroker.Unsubscribe<NetworkMapTrackerPartyCreated>(Handle_NetworkMapTrackerPartyCreated);

        messageBroker.Unsubscribe<MapTrackerPartyRemoved>(Handle_MapTrackerPartyRemoved);
        messageBroker.Unsubscribe<NetworkMapTrackerPartyRemoved>(Handle_NetworkMapTrackerPartyRemoved);
    }

    private void Handle_SwitchedPlayer(MessagePayload<SwitchedPlayer> payload)
    {
        if (holder.Current == null) return;
        holder.Current.ResetTrackers();
    }

    private void Handle_MapTrackerPartyCreated(MessagePayload<MapTrackerPartyCreated> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

        network.SendAll(new NetworkMapTrackerPartyCreated(mobilePartyId));
    }

    private void Handle_NetworkMapTrackerPartyCreated(MessagePayload<NetworkMapTrackerPartyCreated> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;

            holder.Current?.AddIfEligible(mobileParty);
        });
    }

    private void Handle_MapTrackerPartyRemoved(MessagePayload<MapTrackerPartyRemoved> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

        network.SendAll(new NetworkMapTrackerPartyRemoved(mobilePartyId));
    }

    private void Handle_NetworkMapTrackerPartyRemoved(MessagePayload<NetworkMapTrackerPartyRemoved> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;

            holder.Current?.RemoveIfExists(mobileParty);
        });
    }
}