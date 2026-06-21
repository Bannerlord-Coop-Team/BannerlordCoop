using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
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
    private readonly ILogger Logger = LogManager.GetLogger<ServerSettlementExitEnterHandler>();

    public ServerSettlementExitEnterHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager, ISettlementInterface settlementInterface)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.settlementInterface = settlementInterface;
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

        network.Send(peer, new NetworkStartSettlementEncounter(payload));

        // Tell the other clients to apply the entry synchronously here, so the broadcast does not depend
        // on the game-loop pump (the server's own apply below is marshalled onto the game thread).
        network.SendAllBut(peer, new NetworkPartyEnterSettlement(payload.SettlementId, payload.PartyId));

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging(payload.PartyId, out MobileParty mobileParty)) return;
            if (!objectManager.TryGetObjectWithLogging(payload.SettlementId, out Settlement settlement)) return;

            settlementInterface.PartyEnterSettlement(mobileParty, settlement);
        });
    }

    private void Handle(MessagePayload<NetworkRequestEndSettlementEncounter> obj)
    {
        var payload = obj.What;
        var peer = (NetPeer)obj.Who;

        // The sending client is currently in a settlement encounter, this is handled
        // slightly differently from ai or other clients parties
        network.Send(peer, new NetworkEndSettlementEncounter());

        network.SendAllBut(peer, new NetworkPartyLeaveSettlement(payload.PartyId));

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

        network.SendAll(new NetworkPartyEnterSettlement(settlementId, mobilePartyId));

        settlementInterface.OnPartyEnteredSettlement(payload.Settlement, payload.MobileParty);
    }

    private void Handle(MessagePayload<PartyLeaveSettlementAttempted> obj)
    {
        var payload = obj.What;

        if (!objectManager.TryGetIdWithLogging(payload.MobileParty, out var mobilePartyId)) return;

        network.SendAll(new NetworkPartyLeaveSettlement(mobilePartyId));

        settlementInterface.OnPartyLeftSettlement(payload.MobileParty);
    }
}
