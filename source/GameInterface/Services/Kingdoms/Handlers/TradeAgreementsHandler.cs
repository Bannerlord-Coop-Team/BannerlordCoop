using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Kingdoms.Data;
using GameInterface.Services.Kingdoms.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using static TaleWorlds.CampaignSystem.CampaignBehaviors.TradeAgreementsCampaignBehavior;

namespace GameInterface.Services.Kingdoms.Handlers;

internal class TradeAgreementsHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<TradeAgreementsHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;

    public TradeAgreementsHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;

        messageBroker.Subscribe<UpdateTradeAgreement>(Handle_UpdateTradeAgreement);
        messageBroker.Subscribe<NetworkUpdateTradeAgreement>(Handle_NetworkUpdateTradeAgreement);

        messageBroker.Subscribe<ClientAcceptsTradeAgreementOffer>(Handle_ClientAcceptsTradeAgreementOffer);
        messageBroker.Subscribe<NetworkClientAcceptsTradeAgreementOffer>(Handle_NetworkClientAcceptsTradeAgreementOffer);

        messageBroker.Subscribe<TradeGoldDistributedInKingdom>(Handle_TradeGoldDistributedInKingdom);
        messageBroker.Subscribe<NetworkTradeGoldDistributedInKingdom>(Handle_NetworkTradeGoldDistributedInKingdom);

        messageBroker.Subscribe<MakeTradeAgreement>(Handle_MakeTradeAgreement);
        messageBroker.Subscribe<NetworkMakeTradeAgreement>(Handle_NetworkMakeTradeAgreement);

        messageBroker.Subscribe<RemoveTradeAgreement>(Handle_RemoveTradeAgreement);
        messageBroker.Subscribe<NetworkRemoveTradeAgreement>(Handle_NetworkRemoveTradeAgreement);

        messageBroker.Subscribe<EndAllTradeAgreements>(Handle_EndAllTradeAgreements);
        messageBroker.Subscribe<NetworkEndAllTradeAgreements>(Handle_NetworkEndAllTradeAgreements);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<UpdateTradeAgreement>(Handle_UpdateTradeAgreement);
        messageBroker.Unsubscribe<NetworkUpdateTradeAgreement>(Handle_NetworkUpdateTradeAgreement);

        messageBroker.Unsubscribe<ClientAcceptsTradeAgreementOffer>(Handle_ClientAcceptsTradeAgreementOffer);
        messageBroker.Unsubscribe<NetworkClientAcceptsTradeAgreementOffer>(Handle_NetworkClientAcceptsTradeAgreementOffer);

        messageBroker.Unsubscribe<TradeGoldDistributedInKingdom>(Handle_TradeGoldDistributedInKingdom);
        messageBroker.Unsubscribe<NetworkTradeGoldDistributedInKingdom>(Handle_NetworkTradeGoldDistributedInKingdom);

        messageBroker.Unsubscribe<MakeTradeAgreement>(Handle_MakeTradeAgreement);
        messageBroker.Unsubscribe<NetworkMakeTradeAgreement>(Handle_NetworkMakeTradeAgreement);

        messageBroker.Unsubscribe<RemoveTradeAgreement>(Handle_RemoveTradeAgreement);
        messageBroker.Unsubscribe<NetworkRemoveTradeAgreement>(Handle_NetworkRemoveTradeAgreement);

        messageBroker.Unsubscribe<EndAllTradeAgreements>(Handle_EndAllTradeAgreements);
        messageBroker.Unsubscribe<NetworkEndAllTradeAgreements>(Handle_NetworkEndAllTradeAgreements);
    }

    private void Handle_UpdateTradeAgreement(MessagePayload<UpdateTradeAgreement> obj)
    {
        var data = obj.What;

        if (!TryPackTradeAgreementData(data.TradeAgreement, out var tradeAgreementData)) return;

        if (!objectManager.TryGetIdWithLogging(data.Settlement, out var settlementId)) return;
        if (!objectManager.TryGetIdWithLogging(data.MobileParty, out var mobilePartyId)) return;

        var message = new NetworkUpdateTradeAgreement(tradeAgreementData, settlementId, mobilePartyId);
        network.SendAll(message);
    }

    private void Handle_NetworkUpdateTradeAgreement(MessagePayload<NetworkUpdateTradeAgreement> obj)
    {
        var data = obj.What;

        // Update data on all clients
        GameThread.RunSafe(() =>
        {
            if (!TryGetTradeAgreementsBehavior(out var tradeAgreementsBehavior)) return;

            if (!TryUnpackTradeAgreementData(data.TradeAgreementData, out var tradeAgreement)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.What.SettlementId, out var settlement)) return;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;

            if (!tradeAgreementsBehavior.TryGetTradeAgreement((Kingdom)settlement.MapFaction, (Kingdom)mobileParty.MapFaction, out var agreementIndex)) return;

            Kingdom kingdom = (Kingdom)settlement.MapFaction;
            tradeAgreementsBehavior._tradeAgreements[agreementIndex] = tradeAgreement;
        });
    }

    private void Handle_ClientAcceptsTradeAgreementOffer(MessagePayload<ClientAcceptsTradeAgreementOffer> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.FromKingdom, out var fromKingdomId)) return;
        if (!objectManager.TryGetIdWithLogging(data.PlayerKingdom, out var playerKingdomId)) return;

        var message = new NetworkClientAcceptsTradeAgreementOffer(fromKingdomId, playerKingdomId);
        network.SendAll(message);
    }

    private void Handle_NetworkClientAcceptsTradeAgreementOffer(MessagePayload<NetworkClientAcceptsTradeAgreementOffer> obj)
    {
        var data = obj.What;

        // Run new trade agreement on server to use patch, updating all clients as well
        GameThread.RunSafe(() =>
        {
            if (!TryGetTradeAgreementsBehavior(out var tradeAgreementsBehavior)) return;

            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.What.FromKingdomId, out var fromKingdom)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.What.PlayerKingdomId, out var playerKingdom)) return;

            tradeAgreementsBehavior.MakeTradeAgreement(
                fromKingdom,
                playerKingdom,
                Campaign.Current.Models.TradeAgreementModel.GetTradeAgreementDurationInYears(fromKingdom, playerKingdom));
        });
    }

    private void Handle_TradeGoldDistributedInKingdom(MessagePayload<TradeGoldDistributedInKingdom> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.Kingdom1, out var kingdomId)) return;
        if (!objectManager.TryGetIdWithLogging(data.Kingdom2, out var kingdom2Id)) return;
        if (!objectManager.TryGetIdWithLogging(data.Clan, out var clanId)) return;

        var message = new NetworkTradeGoldDistributedInKingdom(kingdomId, kingdom2Id, clanId, data.Share);
        network.SendAll(message);
    }

    private void Handle_NetworkTradeGoldDistributedInKingdom(MessagePayload<NetworkTradeGoldDistributedInKingdom> obj)
    {
        var data = obj.What;

        // Update data on all clients
        GameThread.RunSafe(() =>
        {
            if (!TryGetTradeAgreementsBehavior(out var tradeAgreementsBehavior)) return;

            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.What.Kingdom1Id, out var kingdom1)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.What.Kingdom2Id, out var kingdom2)) return;
            if (!objectManager.TryGetObjectWithLogging<Clan>(obj.What.ClanId, out var clan)) return;

            using (new AllowedThread())
            {
                tradeAgreementsBehavior.OnTradeGoldDistributedInKingdom(kingdom1, kingdom2, clan, data.Share);
            }
        });
    }

    private void Handle_MakeTradeAgreement(MessagePayload<MakeTradeAgreement> obj)
    {
        var data = obj.What;

        if (!TryPackTradeAgreementData(data.NewTradeAgreement, out var newTradeAgreementData)) return;

        var message = new NetworkMakeTradeAgreement(newTradeAgreementData);
        network.SendAll(message);
    }

    private void Handle_NetworkMakeTradeAgreement(MessagePayload<NetworkMakeTradeAgreement> obj)
    {
        var data = obj.What;

        // Update data on all clients
        GameThread.RunSafe(() =>
        {
            if (!TryGetTradeAgreementsBehavior(out var tradeAgreementsBehavior)) return;

            if (!TryUnpackTradeAgreementData(data.NewTradeAgreementData, out var newTradeAgreement)) return;

            tradeAgreementsBehavior._tradeAgreements.Add(newTradeAgreement);
        });
    }

    private void Handle_RemoveTradeAgreement(MessagePayload<RemoveTradeAgreement> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.Kingdom1, out var kingdomId)) return;
        if (!objectManager.TryGetIdWithLogging(data.Kingdom2, out var kingdom2Id)) return;

        var message = new NetworkRemoveTradeAgreement(kingdomId, kingdom2Id);
        network.SendAll(message);
    }

    private void Handle_NetworkRemoveTradeAgreement(MessagePayload<NetworkRemoveTradeAgreement> obj)
    {
        var data = obj.What;

        // Update data on all clients
        GameThread.RunSafe(() =>
        {
            if (!TryGetTradeAgreementsBehavior(out var tradeAgreementsBehavior)) return;

            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.What.Kingdom1Id, out var kingdom1)) return;
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.What.Kingdom2Id, out var kingdom2)) return;

            using (new AllowedThread())
            {
                tradeAgreementsBehavior.RemoveTradeAgreement(kingdom1, kingdom2);
            }
        });
    }

    private void Handle_EndAllTradeAgreements(MessagePayload<EndAllTradeAgreements> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.Kingdom, out var kingdomId)) return;

        var message = new NetworkEndAllTradeAgreements(kingdomId);
        network.SendAll(message);
    }

    private void Handle_NetworkEndAllTradeAgreements(MessagePayload<NetworkEndAllTradeAgreements> obj)
    {
        var data = obj.What;

        // Update data on all clients
        GameThread.RunSafe(() =>
        {
            if (!TryGetTradeAgreementsBehavior(out var tradeAgreementsBehavior)) return;

            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.What.KingdomId, out var kingdom)) return;

            using (new AllowedThread())
            {
                tradeAgreementsBehavior.EndTradeAgreementsOfKingdom(kingdom);
            }
        });
    }

    private bool TryPackTradeAgreementData(TradeAgreement tradeAgreement, out TradeAgreementData tradeAgreementData)
    {
        tradeAgreementData = new();

        if (!objectManager.TryGetIdWithLogging(tradeAgreement.Kingdom1, out var kingdom1Id)) return false;
        if (!objectManager.TryGetIdWithLogging(tradeAgreement.Kingdom2, out var kingdom2Id)) return false;

        tradeAgreementData = new(
            kingdom1Id,
            kingdom2Id,
            tradeAgreement.EndTime._numTicks,
            tradeAgreement.Kingdom1GoldGained,
            tradeAgreement.Kingdom2GoldGained,
            tradeAgreement.Kingdom1GoldGainedTotal,
            tradeAgreement.Kingdom2GoldGainedTotal);

        return true;
    }

    private bool TryUnpackTradeAgreementData(TradeAgreementData tradeAgreementData, out TradeAgreement tradeAgreement)
    {
        tradeAgreement = new();

        if (!objectManager.TryGetObjectWithLogging<Kingdom>(tradeAgreementData.Kingdom1Id, out var kingdom1)) return false;
        if (!objectManager.TryGetObjectWithLogging<Kingdom>(tradeAgreementData.Kingdom2Id, out var kingdom2)) return false;

        var endTime = new CampaignTime(tradeAgreementData.EndTimeNumTicks);
        tradeAgreement = new(kingdom1, kingdom2, endTime)
        {
            Kingdom1GoldGained = tradeAgreementData.Kingdom1GoldGained,
            Kingdom2GoldGained = tradeAgreementData.Kingdom2GoldGained,
            Kingdom1GoldGainedTotal = tradeAgreementData.Kingdom1GoldGainedTotal,
            Kingdom2GoldGainedTotal = tradeAgreementData.Kingdom2GoldGainedTotal
        };

        return true;
    }

    private bool TryGetTradeAgreementsBehavior(out TradeAgreementsCampaignBehavior tradeAgreementsBehavior)
    {
        tradeAgreementsBehavior = Campaign.Current?.GetCampaignBehavior<TradeAgreementsCampaignBehavior>();
        if (tradeAgreementsBehavior != null) return true;

        Logger.Debug("Skipping trade agreements update because the campaign behavior is unavailable.");
        return false;
    }
}
