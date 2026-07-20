using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.Kingdoms;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using static GameInterface.Services.ObjectManager.ObjectManager;
using GameInterface.Services.Settlements.Interfaces;
using LiteNetLib;
using Serilog;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Coop.Core.Server.Services.MobileParties.Handlers;

/// <summary>
/// Handles changes to parties for settlement entry and exit.
/// </summary>
public class ServerSettlementExitEnterHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly ISettlementInterface settlementInterface;
    private readonly IKingdomCreationSettlementTracker settlementTracker;
    private readonly ILogger Logger = LogManager.GetLogger<ServerSettlementExitEnterHandler>();

    public ServerSettlementExitEnterHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        ISettlementInterface settlementInterface,
        IKingdomCreationSettlementTracker settlementTracker)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.settlementInterface = settlementInterface;
        this.settlementTracker = settlementTracker;
        messageBroker.Subscribe<NetworkRequestStartSettlementEncounter>(Handle);
        messageBroker.Subscribe<NetworkRequestEndSettlementEncounter>(Handle);

        messageBroker.Subscribe<PartyEnterSettlementAttempted>(Handle);
        messageBroker.Subscribe<PartyLeaveSettlementAttempted>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkRequestStartSettlementEncounter>(Handle);
        messageBroker.Unsubscribe<NetworkRequestEndSettlementEncounter>(Handle);

        messageBroker.Unsubscribe<PartyEnterSettlementAttempted>(Handle);
        messageBroker.Unsubscribe<PartyLeaveSettlementAttempted>(Handle);
    }

    private void Handle(MessagePayload<NetworkRequestStartSettlementEncounter> obj)
    {
        var payload = obj.What;
        var peer = (NetPeer)obj.Who;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging(payload.PartyId, out MobileParty mobileParty))
            {
                network.Send(peer, new NetworkSettlementEncounterRejected(payload));
                return;
            }
            if (!objectManager.TryGetObjectWithLogging(payload.SettlementId, out Settlement settlement))
            {
                network.Send(peer, new NetworkSettlementEncounterRejected(payload));
                return;
            }

            if (mobileParty.Party?.MapEventSide != null)
            {
                Logger.Warning(
                    "Rejecting settlement entry for party {PartyId} because it is already in a map event",
                    payload.PartyId);
                network.Send(peer, new NetworkSettlementEncounterRejected(payload));
                return;
            }

            if (mobileParty.CurrentSettlement != null)
            {
                if (mobileParty.CurrentSettlement == settlement)
                {
                    network.Send(peer, new NetworkStartSettlementEncounter(payload));
                }
                else
                {
                    Logger.Warning(
                        "Rejecting settlement entry for party {PartyId} because it is already in settlement {SettlementId}",
                        payload.PartyId,
                        objectManager.TryGetId(mobileParty.CurrentSettlement, out var currentSettlementId)
                            ? currentSettlementId
                            : mobileParty.CurrentSettlement.StringId);
                    network.Send(peer, new NetworkSettlementEncounterRejected(payload));
                }
                return;
            }

            network.Send(peer, new NetworkStartSettlementEncounter(payload));
            network.SendAllBut(peer, new NetworkPartyEnterSettlement(
                Compact(payload.SettlementId, typeof(Settlement)),
                Compact(payload.PartyId, typeof(MobileParty))));

            settlementInterface.PartyEnterSettlement(mobileParty, settlement);
        }, context: nameof(NetworkRequestStartSettlementEncounter));
    }

    private void Handle(MessagePayload<NetworkRequestEndSettlementEncounter> obj)
    {
        var payload = obj.What;

        objectManager.TryGetObject<MobileParty>(payload.PartyId, out var mobileParty);
        if (settlementTracker.TryConsumeLeave(mobileParty, payload.PartyId))
        {
            return;
        }

        var peer = (NetPeer)obj.Who;

        // The sending client is currently in a settlement encounter, this is handled
        // slightly differently from ai or other clients parties
        network.Send(peer, new NetworkEndSettlementEncounter());

        network.SendAllBut(peer, new NetworkPartyLeaveSettlement(
            Compact(payload.PartyId, typeof(MobileParty))));

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging(payload.PartyId, out MobileParty mobileParty)) return;

            settlementInterface.PartyLeaveSettlement(mobileParty);
        });
    }

    private void Handle(MessagePayload<PartyEnterSettlementAttempted> obj)
    {
        var payload = obj.What;

        if (!objectManager.TryGetIdWithLogging(payload.Settlement, out var settlementId)) return;
        if (!objectManager.TryGetIdWithLogging(payload.MobileParty, out var mobilePartyId)) return;

        settlementId = Compact(settlementId, typeof(Settlement));
        mobilePartyId = Compact(mobilePartyId, typeof(MobileParty));

        network.SendAll(new NetworkPartyEnterSettlement(settlementId, mobilePartyId));

        settlementInterface.OnPartyEnteredSettlement(payload.Settlement, payload.MobileParty);
    }

    private void Handle(MessagePayload<PartyLeaveSettlementAttempted> obj)
    {
        var payload = obj.What;

        if (!objectManager.TryGetIdWithLogging(payload.MobileParty, out var mobilePartyId)) return;

        if (settlementTracker.TryConsumeLeave(payload.MobileParty, mobilePartyId))
        {
            return;
        }
        network.SendAll(new NetworkPartyLeaveSettlement(
            Compact(mobilePartyId, typeof(MobileParty))));

        settlementInterface.OnPartyLeftSettlement(payload.MobileParty);
    }
}
