using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.SiegeEvents.Messages;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using GameInterface.Services.BesiegerCamps.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEvents.Interfaces;
using GameInterface.Services.SiegeEvents.Messages;
using LiteNetLib;
using Serilog;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Coop.Core.Server.Services.SiegeEvents.Handlers;

/// <summary>
/// Runs client siege entry and exit requests authoritatively. The approval is sent from inside the
/// game-thread closure after the world change, so the reliable-ordered channel delivers the siege
/// object creates and camp writes to the requester before its local menu continuation runs.
/// </summary>
internal class ServerSiegeEntryHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerSiegeEntryHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly ISiegeEventInterface siegeEventInterface;

    public ServerSiegeEntryHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager, ISiegeEventInterface siegeEventInterface)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.siegeEventInterface = siegeEventInterface;
        messageBroker.Subscribe<NetworkRequestBesiegeSettlement>(HandleBesiege);
        messageBroker.Subscribe<NetworkRequestJoinSiegeCamp>(HandleJoin);
        messageBroker.Subscribe<NetworkRequestBreakSiege>(HandleBreak);
        messageBroker.Subscribe<SiegeAssaultStarted>(HandleAssaultStarted);
        messageBroker.Subscribe<SiegeCampPositionRolled>(HandleCampPosition);
    }

    // Runs on the game thread already — published from the StartBattleAction patch; only resolves ids and broadcasts, so no GameThread.RunSafe.
    private void HandleAssaultStarted(MessagePayload<SiegeAssaultStarted> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.AttackerParty, out var attackerPartyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.Settlement, out var settlementId)) return;

        // Broadcast; each client checks locally whether its party is inside the settlement.
        network.SendAll(new NetworkPromptSiegeDefense(attackerPartyId, settlementId));
    }

    // Runs on the game thread already — published from the party-joined-siege patch; only resolves an id and broadcasts, so no GameThread.RunSafe.
    private void HandleCampPosition(MessagePayload<SiegeCampPositionRolled> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.Party, out var partyId)) return;

        network.SendAll(new NetworkSnapSiegeCampPartyPosition(partyId, obj.Position));
    }

    private void HandleBesiege(MessagePayload<NetworkRequestBesiegeSettlement> payload)
    {
        var obj = payload.What;
        var peer = (NetPeer)payload.Who;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.PartyId, out var party)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.SettlementId, out var settlement)) return;

            if (settlement.SiegeEvent != null)
            {
                Logger.Error("Party {PartyId} tried to besiege {SettlementId} which is already under siege", obj.PartyId, obj.SettlementId);
                network.Send(peer, new NetworkBesiegeSettlementApproved(false));
                return;
            }

            siegeEventInterface.StartSiegeEvent(party, settlement);

            network.Send(peer, new NetworkBesiegeSettlementApproved(true));
        });
    }

    private void HandleJoin(MessagePayload<NetworkRequestJoinSiegeCamp> payload)
    {
        var obj = payload.What;
        var peer = (NetPeer)payload.Who;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.PartyId, out var party)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.SettlementId, out var settlement)) return;

            if (settlement.SiegeEvent == null || !settlement.SiegeEvent.CanPartyJoinSide(party.Party, BattleSideEnum.Attacker))
            {
                Logger.Error("Party {PartyId} cannot join the siege of {SettlementId}", obj.PartyId, obj.SettlementId);
                network.Send(peer, new NetworkJoinSiegeCampApproved(obj.SettlementId, false));
                return;
            }

            siegeEventInterface.JoinSiegeCamp(party, settlement);

            network.Send(peer, new NetworkJoinSiegeCampApproved(obj.SettlementId, true));
        });
    }

    private void HandleBreak(MessagePayload<NetworkRequestBreakSiege> payload)
    {
        var obj = payload.What;
        var peer = (NetPeer)payload.Who;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.PartyId, out var party)) return;

            if (party.BesiegerCamp == null)
            {
                Logger.Error("Party {PartyId} tried to leave a siege camp it is not in", obj.PartyId);
                network.Send(peer, new NetworkBreakSiegeApproved(false));
                return;
            }

            siegeEventInterface.BreakSiege(party);

            network.Send(peer, new NetworkBreakSiegeApproved(true));
        });
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkRequestBesiegeSettlement>(HandleBesiege);
        messageBroker.Unsubscribe<NetworkRequestJoinSiegeCamp>(HandleJoin);
        messageBroker.Unsubscribe<NetworkRequestBreakSiege>(HandleBreak);
        messageBroker.Unsubscribe<SiegeAssaultStarted>(HandleAssaultStarted);
        messageBroker.Unsubscribe<SiegeCampPositionRolled>(HandleCampPosition);
    }
}
