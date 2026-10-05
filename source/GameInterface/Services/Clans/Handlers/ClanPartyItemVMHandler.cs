using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Clans.Data;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Heroes.Extensions;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Clans.Handlers;

internal class ClanPartyItemVMHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ClanPartyItemVMHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public ClanPartyItemVMHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<PartyConfigurationChangedOnSelection>(Handle_PartyConfigurationChangedOnSelection);
        messageBroker.Subscribe<UpdatePartyConfigurationOnSelection>(Handle_UpdatePartyConfigurationOnSelection);
        messageBroker.Subscribe<AutoRecruitChangedForSettlement>(Handle_AutoRecruitChangedForSettlement);
        messageBroker.Subscribe<ChangeAutoRecruitForSettlement>(Handle_ChangeAutoRecruitForSettlement);
        messageBroker.Subscribe<ChangeAutoRecruitForSettlementClients>(Handle_ChangeAutoRecruitForSettlementClients);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PartyConfigurationChangedOnSelection>(Handle_PartyConfigurationChangedOnSelection);
        messageBroker.Unsubscribe<UpdatePartyConfigurationOnSelection>(Handle_UpdatePartyConfigurationOnSelection);
        messageBroker.Unsubscribe<AutoRecruitChangedForSettlement>(Handle_AutoRecruitChangedForSettlement);
        messageBroker.Unsubscribe<ChangeAutoRecruitForSettlement>(Handle_ChangeAutoRecruitForSettlement);
        messageBroker.Unsubscribe<ChangeAutoRecruitForSettlementClients>(Handle_ChangeAutoRecruitForSettlementClients);
    }

    private void Handle_PartyConfigurationChangedOnSelection(MessagePayload<PartyConfigurationChangedOnSelection> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.Leader, out var leaderHeroId)) return;

        network.SendAll(new UpdatePartyConfigurationOnSelection(leaderHeroId, obj.What.Flag, obj.What.Value));
    }

    private void Handle_UpdatePartyConfigurationOnSelection(MessagePayload<UpdatePartyConfigurationOnSelection> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(obj.What.LeaderHeroId, out var leader)) return;
            if (leader.IsPlayerHero()) return;
            PartyConfigurationFlags.Set(leader, obj.What.Flag, obj.What.Value);
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
