using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Coalescing;
using Common.Network.Messages;
using Coop.Core.Client.Services.SiegeEvents.Messages;
using Coop.Core.Common.Services.SiegeEvents;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using GameInterface.Services.BesiegerCamps.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.SiegeEvents.Interfaces;
using GameInterface.Services.SiegeEvents.Messages;
using GameInterface.Services.SiegeEvents.Validation;
using LiteNetLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using static GameInterface.Services.ObjectManager.ObjectManager;

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
    private readonly IPlayerManager playerManager;
    private readonly ISendCoalescer sendCoalescer;
    private readonly ISiegeEventInterface siegeEventInterface;
    private readonly ISiegeInteractionGrantStore siegeInteractionGrantStore;
    private readonly ISiegeEntryValidator siegeEntryValidator;

    public ServerSiegeEntryHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        ISendCoalescer sendCoalescer,
        ISiegeEventInterface siegeEventInterface,
        ISiegeInteractionGrantStore siegeInteractionGrantStore,
        ISiegeEntryValidator siegeEntryValidator)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.sendCoalescer = sendCoalescer;
        this.siegeEventInterface = siegeEventInterface;
        this.siegeInteractionGrantStore = siegeInteractionGrantStore;
        this.siegeEntryValidator = siegeEntryValidator;
        messageBroker.Subscribe<NetworkRequestBesiegeSettlement>(HandleBesiege);
        messageBroker.Subscribe<NetworkRequestJoinSiegeCamp>(HandleJoin);
        messageBroker.Subscribe<NetworkRequestBreakSiege>(HandleBreak);
        messageBroker.Subscribe<NetworkRequestSiegeAssault>(HandleAssault);
        messageBroker.Subscribe<SiegeAssaultStarted>(HandleAssaultStarted);
        messageBroker.Subscribe<SiegePreparationStarted>(HandlePreparationStarted);
        messageBroker.Subscribe<SiegeEndedWithoutBattle>(HandleSiegeEnded);
        messageBroker.Subscribe<SiegeCampPositionRolled>(HandleCampPosition);
        messageBroker.Subscribe<PlayerCampaignEntered>(HandlePlayerCampaignEntered);
        messageBroker.Subscribe<PlayerDisconnected>(HandlePlayerDisconnected);
    }

    // Runs on the game thread already; joins defenders with patches live before broadcasting the prompts.
    private void HandleAssaultStarted(MessagePayload<SiegeAssaultStarted> payload)
    {
        var obj = payload.What;

        JoinConnectedSettlementDefenders(obj.AttackerParty, obj.Settlement);

        if (!objectManager.TryGetIdWithLogging(obj.AttackerParty, out var attackerPartyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.Settlement, out var settlementId)) return;

        // Broadcast; each client checks locally whether its party is inside the settlement.
        network.SendAll(new NetworkPromptSiegeDefense(attackerPartyId, settlementId));
        // Also prompt the besieging players to adopt the replicated assault as their encounter so they can enter it.
        network.SendAll(new NetworkPromptSiegeAssault(attackerPartyId, settlementId));
    }

    private void JoinConnectedSettlementDefenders(MobileParty attackerParty, Settlement settlement)
    {
        var mapEvent = attackerParty?.MapEvent;
        var defenderSide = mapEvent?.DefenderSide;
        if (defenderSide == null) return;

        foreach (var player in playerManager.Players)
        {
            if (!playerManager.IsConnected(player)) continue;
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party)) continue;
            if (party.CurrentSettlement != settlement || party.Party.MapEventSide != null) continue;
            if (!mapEvent.CanPartyJoinBattle(party.Party, BattleSideEnum.Defender)) continue;

            party.Party.MapEventSide = defenderSide;
        }
    }

    // Runs on the game thread already — published from the StartSiegeEvent postfix, after the whole siege
    // graph was broadcast, so the prompt arrives behind it on the reliable-ordered channel.
    private void HandlePreparationStarted(MessagePayload<SiegePreparationStarted> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.BesiegerParty, out var attackerPartyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.Settlement, out var settlementId)) return;

        // Broadcast; each client checks locally whether its party is inside the settlement.
        network.SendAll(new NetworkPromptSiegePreparation(attackerPartyId, settlementId));
    }

    // Runs on the game thread already — published from the FinalizeSiegeEvent postfix, behind the
    // replicated siege teardown.
    private void HandleSiegeEnded(MessagePayload<SiegeEndedWithoutBattle> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.Settlement, out var settlementId)) return;

        network.SendAll(new NetworkPromptSiegeEnded(settlementId, obj.BesiegerDefeated));
    }

    private void HandleAssault(MessagePayload<NetworkRequestSiegeAssault> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.PartyId, out _)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(obj.SettlementId, out var settlement)) return;

            var camp = settlement.SiegeEvent?.BesiegerCamp;
            if (camp == null)
            {
                Logger.Error("Party {PartyId} tried to assault {SettlementId} which is not under siege", obj.PartyId, obj.SettlementId);
                return;
            }

            // Create the assault authoritatively with patches LIVE so the map event registers + replicates and
            // SiegeAssaultPromptPatches fires SiegeAssaultStarted (broadcasting the attacker/defender prompts). The
            // camp leader is the authoritative attacker, matching vanilla lead_assault_on_consequence.
            if (settlement.Party.MapEvent == null)
            {
                // SiegeEntryFlowPatches only reroutes the assault menu consequence, so the vanilla
                // preparation-complete on_condition is bypassed; enforce it authoritatively here.
                if (!camp.IsPreparationComplete)
                {
                    Logger.Warning("Party {PartyId} tried to assault {SettlementId} before siege preparations completed", obj.PartyId, obj.SettlementId);
                    return;
                }

                StartBattleAction.ApplyStartAssaultAgainstWalls(camp.LeaderParty, settlement);
                return;
            }

            // Assault already live (e.g. a repeat click): re-broadcast the prompt so a besieger still catching up enters it.
            if (settlement.Party.MapEvent.IsSiegeAssault && objectManager.TryGetId(camp.LeaderParty, out var leaderId))
            {
                network.SendAll(new NetworkPromptSiegeAssault(leaderId, obj.SettlementId));
            }
        });
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

        GameThread.RunSafe(
            () => HandleEntry(
                peer,
                obj.PartyId,
                obj.SettlementId,
                obj.InteractionId,
                SiegeEntryRequestType.Besiege,
                SiegeEntryAction.Besiege),
            context: nameof(NetworkRequestBesiegeSettlement));
    }

    private void HandleJoin(MessagePayload<NetworkRequestJoinSiegeCamp> payload)
    {
        var obj = payload.What;
        var peer = (NetPeer)payload.Who;

        GameThread.RunSafe(
            () => HandleEntry(
                peer,
                obj.PartyId,
                obj.SettlementId,
                obj.InteractionId,
                SiegeEntryRequestType.Join,
                SiegeEntryAction.Join),
            context: nameof(NetworkRequestJoinSiegeCamp));
    }

    private void HandleEntry(
        NetPeer peer,
        string partyId,
        string settlementId,
        string interactionId,
        SiegeEntryRequestType requestType,
        SiegeEntryAction action)
    {
        if (!playerManager.TryGetPlayer(peer, out var player) ||
            player.MobilePartyId != partyId)
        {
            SendEntryResult(
                peer,
                partyId,
                settlementId,
                interactionId,
                requestType,
                SiegeEntryOutcome.Rejected,
                SiegeEntryDenialReason.InvalidRequester,
                new SiegeEntryCanonicalState(SiegeEntryDisposition.Map, null));
            return;
        }

        if (!objectManager.TryGetObjectWithLogging<MobileParty>(partyId, out var party))
        {
            SendEntryResult(
                peer,
                partyId,
                settlementId,
                interactionId,
                requestType,
                SiegeEntryOutcome.Rejected,
                SiegeEntryDenialReason.InvalidParty,
                new SiegeEntryCanonicalState(SiegeEntryDisposition.Map, null));
            return;
        }

        if (!objectManager.TryGetObjectWithLogging<Settlement>(settlementId, out var settlement))
        {
            SendEntryResult(
                peer,
                partyId,
                settlementId,
                interactionId,
                requestType,
                SiegeEntryOutcome.Rejected,
                SiegeEntryDenialReason.InvalidSettlement,
                siegeEntryValidator.GetCanonicalState(party));
            return;
        }

        if (!siegeInteractionGrantStore.TryConsume(
                peer,
                interactionId,
                partyId,
                settlementId,
                settlement.SiegeEvent?.BesiegerCamp))
        {
            SendEntryResult(
                peer,
                partyId,
                settlementId,
                interactionId,
                requestType,
                SiegeEntryOutcome.Rejected,
                SiegeEntryDenialReason.MissingInteractionGrant,
                siegeEntryValidator.GetCanonicalState(party));
            return;
        }

        var validation = siegeEntryValidator.ValidateEntry(party, settlement, action);
        if (!validation.IsValid)
        {
            if (validation.Reason == SiegeEntryDenialReason.MovementTargetMismatch ||
                validation.Reason == SiegeEntryDenialReason.TooFar ||
                validation.Reason == SiegeEntryDenialReason.DefenderDisposition)
            {
                StopApproachAndFlushBehavior(party, partyId);
            }

            Logger.Warning(
                "Rejected {RequestType} entry for party {PartyId} at {SettlementId}: {Reason}",
                requestType,
                partyId,
                settlementId,
                validation.Reason);
            SendEntryResult(
                peer,
                partyId,
                settlementId,
                interactionId,
                requestType,
                SiegeEntryOutcome.Rejected,
                validation.Reason,
                validation.CanonicalState);
            return;
        }

        try
        {
            if (action == SiegeEntryAction.Besiege)
            {
                siegeEventInterface.StartSiegeEvent(party, settlement);
            }
            else
            {
                siegeEventInterface.JoinSiegeCamp(party, settlement);
            }
        }
        catch (Exception exception)
        {
            Logger.Error(
                exception,
                "Failed to apply {RequestType} entry for party {PartyId} at {SettlementId}",
                requestType,
                partyId,
                settlementId);
            SendEntryResult(
                peer,
                partyId,
                settlementId,
                interactionId,
                requestType,
                SiegeEntryOutcome.Rejected,
                SiegeEntryDenialReason.ActionFailed,
                siegeEntryValidator.GetCanonicalState(party));
            return;
        }

        SendEntryResult(
            peer,
            partyId,
            settlementId,
            interactionId,
            requestType,
            SiegeEntryOutcome.Applied,
            SiegeEntryDenialReason.None,
            siegeEntryValidator.GetCanonicalState(party));
    }

    private void HandlePlayerCampaignEntered(MessagePayload<PlayerCampaignEntered> payload)
    {
        var peer = payload.What.playerId;
        if (!playerManager.TryGetPlayer(peer, out var player) ||
            !objectManager.TryGetObjectWithLogging<MobileParty>(player.MobilePartyId, out var party))
        {
            return;
        }

        var validation = siegeEntryValidator.ValidateReloadedBesieger(party);
        if (!validation.IsValid)
        {
            Logger.Warning(
                "Repairing stale siege linkage for reconnecting party {PartyId}",
                player.MobilePartyId);
            if (siegeEventInterface.BreakSiegeForPartyOnly(party))
            {
                network.SendAll(
                    new NetworkClearStaleBesiegerCamp(player.MobilePartyId));
            }
            StopApproachAndFlushBehavior(party, player.MobilePartyId);
        }

        var canonicalState = siegeEntryValidator.GetCanonicalState(party);
        SendEntryResult(
            peer,
            player.MobilePartyId,
            GetSettlementId(canonicalState.Settlement),
            null,
            SiegeEntryRequestType.Reconnect,
            validation.IsValid ? SiegeEntryOutcome.Applied : SiegeEntryOutcome.Rejected,
            validation.Reason,
            canonicalState);
    }

    private void HandlePlayerDisconnected(MessagePayload<PlayerDisconnected> payload)
    {
        siegeInteractionGrantStore.Revoke(payload.What.PlayerId);
    }

    private void StopApproachAndFlushBehavior(MobileParty party, string partyId)
    {
        party.SetMoveModeHold();
        sendCoalescer.FlushInstance(Compact(partyId, typeof(MobileParty)), network);
    }

    private void SendEntryResult(
        NetPeer peer,
        string partyId,
        string requestedSettlementId,
        string interactionId,
        SiegeEntryRequestType requestType,
        SiegeEntryOutcome outcome,
        SiegeEntryDenialReason reason,
        SiegeEntryCanonicalState canonicalState)
    {
        network.Send(peer, new NetworkSiegeEntryResult(
            partyId,
            requestedSettlementId,
            interactionId,
            requestType,
            outcome,
            reason,
            canonicalState.Disposition,
            GetSettlementId(canonicalState.Settlement)));
    }

    private string GetSettlementId(Settlement settlement)
    {
        if (settlement == null)
            return null;

        return objectManager.TryGetId(settlement, out var settlementId)
            ? settlementId
            : null;
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
                if (party.MapEvent?.IsSiegeAssault == true && party.Party.Side == BattleSideEnum.Attacker)
                {
                    messageBroker.Publish(party, new PlayerLeaveBattleAttempted(party.Party));
                    network.Send(peer, new NetworkBreakSiegeApproved(true, true));
                    return;
                }

                Logger.Error("Party {PartyId} tried to leave a siege camp it is not in", obj.PartyId);
                network.Send(peer, new NetworkBreakSiegeApproved(false, false));
                return;
            }

            siegeEventInterface.BreakSiege(party);

            network.Send(peer, new NetworkBreakSiegeApproved(true, false));
        });
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkRequestBesiegeSettlement>(HandleBesiege);
        messageBroker.Unsubscribe<NetworkRequestJoinSiegeCamp>(HandleJoin);
        messageBroker.Unsubscribe<NetworkRequestBreakSiege>(HandleBreak);
        messageBroker.Unsubscribe<NetworkRequestSiegeAssault>(HandleAssault);
        messageBroker.Unsubscribe<SiegeAssaultStarted>(HandleAssaultStarted);
        messageBroker.Unsubscribe<SiegePreparationStarted>(HandlePreparationStarted);
        messageBroker.Unsubscribe<SiegeEndedWithoutBattle>(HandleSiegeEnded);
        messageBroker.Unsubscribe<SiegeCampPositionRolled>(HandleCampPosition);
        messageBroker.Unsubscribe<PlayerCampaignEntered>(HandlePlayerCampaignEntered);
        messageBroker.Unsubscribe<PlayerDisconnected>(HandlePlayerDisconnected);
    }
}
