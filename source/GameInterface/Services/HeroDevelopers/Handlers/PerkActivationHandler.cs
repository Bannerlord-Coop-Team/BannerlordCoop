using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.HeroDevelopers.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.HeroDevelopers.Handlers;

internal class PerkActivationHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<PerkActivationHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public PerkActivationHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<UpdateMemberRosterVersionAfterPerkChange>(Handle_UpdateMemberRosterVersionAfterPerkActivation);
        messageBroker.Subscribe<NetworkUpdateMemberRosterVersionAfterPerkChange>(Handle_NetworkUpdateMemberRosterVersionAfterPerkActivation);
        messageBroker.Subscribe<UpdateItemRosterVersionAfterPerkChange>(Handle_UpdateItemRosterVersionAfterPerkActivation);
        messageBroker.Subscribe<NetworkUpdateItemRosterVersionAfterPerkChange>(Handle_NetworkUpdateItemRosterVersionAfterPerkActivation);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<UpdateMemberRosterVersionAfterPerkChange>(Handle_UpdateMemberRosterVersionAfterPerkActivation);
        messageBroker.Unsubscribe<NetworkUpdateMemberRosterVersionAfterPerkChange>(Handle_NetworkUpdateMemberRosterVersionAfterPerkActivation);
        messageBroker.Unsubscribe<UpdateItemRosterVersionAfterPerkChange>(Handle_UpdateItemRosterVersionAfterPerkActivation);
        messageBroker.Unsubscribe<NetworkUpdateItemRosterVersionAfterPerkChange>(Handle_NetworkUpdateItemRosterVersionAfterPerkActivation);
    }

    private void Handle_UpdateMemberRosterVersionAfterPerkActivation(MessagePayload<UpdateMemberRosterVersionAfterPerkChange> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(data.MemberRoster, out var memberRosterId)) return;

            var message = new NetworkUpdateMemberRosterVersionAfterPerkChange(memberRosterId);
            network.SendAll(message);
        });
    }

    private void Handle_NetworkUpdateMemberRosterVersionAfterPerkActivation(MessagePayload<NetworkUpdateMemberRosterVersionAfterPerkChange> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<TroopRoster>(data.MemberRosterId, out var memberRoster)) return;

            memberRoster.UpdateVersion();
        });
    }

    private void Handle_UpdateItemRosterVersionAfterPerkActivation(MessagePayload<UpdateItemRosterVersionAfterPerkChange> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(data.ItemRoster, out var itemRosterId)) return;

            var message = new NetworkUpdateItemRosterVersionAfterPerkChange(itemRosterId);
            network.SendAll(message);
        });
    }

    private void Handle_NetworkUpdateItemRosterVersionAfterPerkActivation(MessagePayload<NetworkUpdateItemRosterVersionAfterPerkChange> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<ItemRoster>(data.ItemRosterId, out var itemRoster)) return;

            itemRoster.UpdateVersion();
        });
    }
}
