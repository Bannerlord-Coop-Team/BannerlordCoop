using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Serilog;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Clans.Handlers;

internal class ClanPartyItemVMHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ClanPartyItemVMHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly ICoopClanPermissions permissions;

    public ClanPartyItemVMHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        ICoopClanPermissions permissions)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.permissions = permissions;

        messageBroker.Subscribe<PartyBehaviorUpdatedOnSelection>(Handle_PartyBehaviorUpdatedOnSelection);
        messageBroker.Subscribe<UpdatePartyBehaviorOnSelection>(Handle_UpdatePartyBehaviorOnSelection);
        messageBroker.Subscribe<AutoRecruitChangedForSettlement>(Handle_AutoRecruitChangedForSettlement);
        messageBroker.Subscribe<ChangeAutoRecruitForSettlement>(Handle_ChangeAutoRecruitForSettlement);
        messageBroker.Subscribe<ChangeAutoRecruitForSettlementClients>(Handle_ChangeAutoRecruitForSettlementClients);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyBehaviorUpdatedOnSelection>(Handle_PartyBehaviorUpdatedOnSelection);
        messageBroker.Unsubscribe<UpdatePartyBehaviorOnSelection>(Handle_UpdatePartyBehaviorOnSelection);
        messageBroker.Unsubscribe<AutoRecruitChangedForSettlement>(Handle_AutoRecruitChangedForSettlement);
        messageBroker.Unsubscribe<ChangeAutoRecruitForSettlement>(Handle_ChangeAutoRecruitForSettlement);
        messageBroker.Unsubscribe<ChangeAutoRecruitForSettlementClients>(Handle_ChangeAutoRecruitForSettlementClients);
    }

    private void Handle_PartyBehaviorUpdatedOnSelection(MessagePayload<PartyBehaviorUpdatedOnSelection> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

        network.SendAll(new UpdatePartyBehaviorOnSelection(mobilePartyId, obj.What.PartyObjective));
    }

    private void Handle_UpdatePartyBehaviorOnSelection(MessagePayload<UpdatePartyBehaviorOnSelection> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;
            if (mobileParty.IsPlayerParty()) return;
            if (!playerManager.TryGetSenderHero(obj.Who, out var senderHero) || !permissions.CanManageParty(senderHero, mobileParty)) return;
            mobileParty.SetPartyObjective(obj.What.PartyObjective);
        });
    }

    private void Handle_AutoRecruitChangedForSettlement(MessagePayload<AutoRecruitChangedForSettlement> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.HomeSettlement, out var homeSettlementId)) return;

        network.SendAll(new ChangeAutoRecruitForSettlement(homeSettlementId, obj.What.Value));
    }

    private void Handle_ChangeAutoRecruitForSettlement(MessagePayload<ChangeAutoRecruitForSettlement> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.What.HomeSettlementId, out var homeSettlement)) return;
            if (homeSettlement.Town == null) return;
            if (!playerManager.IsSenderControllingClan(obj.Who, homeSettlement.OwnerClan)) return;
            if (!playerManager.TryGetSenderHero(obj.Who, out var senderHero) || !permissions.CanManageClan(senderHero, homeSettlement.OwnerClan)) return;

            homeSettlement.Town.GarrisonAutoRecruitmentIsEnabled = obj.What.Value;
            network.SendAll(new ChangeAutoRecruitForSettlementClients(obj.What.HomeSettlementId, obj.What.Value));
        });
    }

    private void Handle_ChangeAutoRecruitForSettlementClients(MessagePayload<ChangeAutoRecruitForSettlementClients> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.What.HomeSettlementId, out var homeSettlement)) return;
            homeSettlement.Town.GarrisonAutoRecruitmentIsEnabled = obj.What.Value;
        });
    }
}
