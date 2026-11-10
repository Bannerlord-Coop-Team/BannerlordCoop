using Common;
using Common.Messaging;
using Common.Network;
using Coop.Naval.Notifications.Messages;
using GameInterface.Services.ObjectManager;
using TaleWorlds.CampaignSystem.Roster;

namespace Coop.Naval.Notifications.Handlers;

/// <summary>
/// Replicates the item roster version bump that opening a naval Boatswain perk causes, so clients recompute roster data.
/// </summary>
internal class NavalItemRosterVersionHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public NavalItemRosterVersionHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<UpdateItemRosterVersionAfterPerkChange>(Handle_UpdateItemRosterVersionAfterPerkChange);
        messageBroker.Subscribe<NetworkUpdateItemRosterVersionAfterPerkChange>(Handle_NetworkUpdateItemRosterVersionAfterPerkChange);
    }

    /// <summary>Stops listening for roster version updates.</summary>
    public void Dispose()
    {
        messageBroker.Unsubscribe<UpdateItemRosterVersionAfterPerkChange>(Handle_UpdateItemRosterVersionAfterPerkChange);
        messageBroker.Unsubscribe<NetworkUpdateItemRosterVersionAfterPerkChange>(Handle_NetworkUpdateItemRosterVersionAfterPerkChange);
    }

    /// <summary>Tells clients which item roster the server bumped.</summary>
    private void Handle_UpdateItemRosterVersionAfterPerkChange(MessagePayload<UpdateItemRosterVersionAfterPerkChange> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(data.ItemRoster, out var itemRosterId)) return;

            network.SendAll(new NetworkUpdateItemRosterVersionAfterPerkChange(itemRosterId));
        });
    }

    /// <summary>Bumps the item roster version the server sent.</summary>
    private void Handle_NetworkUpdateItemRosterVersionAfterPerkChange(MessagePayload<NetworkUpdateItemRosterVersionAfterPerkChange> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<ItemRoster>(data.ItemRosterId, out var itemRoster)) return;

            itemRoster.UpdateVersion();
        });
    }
}
