using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Client.Services.SiegeEvents.Messages;
using Coop.Core.Server.Services.Settlements;
using Coop.Core.Server.Services.SiegeEvents.Messages;
using GameInterface.Services.BesiegerCamps.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Settlements.Interfaces;
using GameInterface.Services.SiegeEvents.Interfaces;
using GameInterface.Services.SiegeEvents.Messages;
using LiteNetLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
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
    private readonly ISettlementEncounterDistanceValidator distanceValidator;
    private readonly ISiegeEventInterface siegeEventInterface;
    private readonly ISettlementInterface settlementInterface;

    public ServerSiegeEntryHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        ISettlementEncounterDistanceValidator distanceValidator,
        ISiegeEventInterface siegeEventInterface,
        ISettlementInterface settlementInterface)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.distanceValidator = distanceValidator;
        this.siegeEventInterface = siegeEventInterface;
        this.settlementInterface = settlementInterface;
        messageBroker.Subscribe<NetworkRequestBesiegeSettlement>(HandleBesiege);
        messageBroker.Subscribe<NetworkRequestJoinSiegeCamp>(HandleJoin);
        messageBroker.Subscribe<NetworkRequestBreakSiege>(HandleBreak);
        messageBroker.Subscribe<NetworkRequestSiegeAssault>(HandleAssault);
        messageBroker.Subscribe<SiegeAssaultStarted>(HandleAssaultStarted);
        messageBroker.Subscribe<SiegePreparationStarted>(HandlePreparationStarted);
        messageBroker.Subscribe<SiegeEndedWithoutBattle>(HandleSiegeEnded);
        messageBroker.Subscribe<SiegeCampPositionRolled>(HandleCampPosition);
        messageBroker.Subscribe<NetworkRequestBreakInContinuation>(HandleBreakInContinuation);
    }

    private void HandleBreakInContinuation(MessagePayload<NetworkRequestBreakInContinuation> payload)
    {
        var obj = payload.What;
        if (!(payload.Who is NetPeer peer))
        {
            Logger.Error("Received {Message} with no originating peer", nameof(NetworkRequestBreakInContinuation));
            return;
        }

        GameThread.RunSafe(() =>
        {
            bool approved;
            try
            {
                approved = TryApplyBreakInContinuation(peer, obj);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply break-in continuation request {RequestId}", obj.RequestId);
                approved = false;
            }

            SendBreakInContinuationResult(peer, obj, approved);
        }, context: nameof(HandleBreakInContinuation));
    }

    private bool TryApplyBreakInContinuation(
        NetPeer peer,
        NetworkRequestBreakInContinuation request)
    {
        if (!DoesPeerControlParty(peer, request)) return false;

        if (!objectManager.TryGetObjectWithLogging<MobileParty>(request.PartyId, out var party) ||
            !objectManager.TryGetObjectWithLogging<Settlement>(request.SettlementId, out var settlement))
            return false;

        var alreadyEntered = ReferenceEquals(party.CurrentSettlement, settlement);
        if (!CanApplyBreakInContinuation(party, settlement, alreadyEntered))
        {
            Logger.Warning("Rejecting break-in request {RequestId} for party {PartyId} and settlement {SettlementId}",
                request.RequestId, request.PartyId, request.SettlementId);
            return false;
        }

        if (alreadyEntered)
        {
            network.Send(peer, new NetworkPartyEnterSettlement(
                Compact(request.SettlementId, typeof(Settlement)),
                Compact(request.PartyId, typeof(MobileParty))));
        }
        else
        {
            settlementInterface.PartyEnterSettlement(party, settlement);
        }

        return ReferenceEquals(party.CurrentSettlement, settlement);
    }

    private bool DoesPeerControlParty(
        NetPeer peer,
        NetworkRequestBreakInContinuation request)
    {
        if (playerManager.TryGetPlayer(peer, out var player) &&
            player.MobilePartyId == request.PartyId)
            return true;

        Logger.Warning("Rejecting break-in request {RequestId} from a peer that does not control party {PartyId}",
            request.RequestId, request.PartyId);
        return false;
    }

    private static bool CanApplyBreakInContinuation(
        MobileParty party,
        Settlement settlement,
        bool alreadyEntered)
    {
        if (!party.IsActive) return false;
        if (party.CurrentSettlement != null && !alreadyEntered) return false;
        if (party.BesiegerCamp != null) return false;

        var partyBase = party.Party;
        var mapEventSide = partyBase.MapEventSide;
        var validEnteredMapEvent = alreadyEntered &&
            ReferenceEquals(party.MapEvent, settlement.Party?.MapEvent) &&
            partyBase.Side == BattleSideEnum.Defender;
        if (mapEventSide != null && !validEnteredMapEvent) return false;

        var siegeEvent = settlement.SiegeEvent;
        return siegeEvent != null &&
            siegeEvent.CanPartyJoinSide(partyBase, BattleSideEnum.Defender);
    }

    private void SendBreakInContinuationResult(
        NetPeer peer,
        NetworkRequestBreakInContinuation request,
        bool approved)
    {
        network.Send(peer, new NetworkBreakInContinuationApproved(
            request.RequestId,
            request.SettlementId,
            approved));
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
                SiegeEntryAction.Join),
            context: nameof(NetworkRequestJoinSiegeCamp));
    }

    private void HandleEntry(
        NetPeer peer,
        string partyId,
        string settlementId,
        SiegeEntryAction action)
    {
        if (!playerManager.TryGetPlayer(peer, out var player) ||
            player.MobilePartyId != partyId)
        {
            RejectEntry(peer, partyId, settlementId, action, "your party is not controlled by you");
            return;
        }

        if (!objectManager.TryGetObjectWithLogging<MobileParty>(partyId, out var party) ||
            !objectManager.TryGetObjectWithLogging<Settlement>(settlementId, out var settlement))
        {
            SendEntryRejection(
                peer,
                settlementId,
                action,
                "your party or the settlement is no longer available");
            return;
        }

        try
        {
            var targetCamp = settlement.SiegeEvent?.BesiegerCamp;
            if (targetCamp != null &&
                ReferenceEquals(party.BesiegerCamp, targetCamp))
            {
                SendEntryResult(peer, settlementId, action, approved: true);
                return;
            }

            if (!TryValidateEntry(party, settlement, action, out var rejectionReason))
            {
                RejectEntry(peer, partyId, settlementId, action, rejectionReason);
                return;
            }

            if (action == SiegeEntryAction.Besiege)
                siegeEventInterface.StartSiegeEvent(party, settlement);
            else
                siegeEventInterface.JoinSiegeCamp(party, settlement);
        }
        catch (Exception exception)
        {
            Logger.Error(
                exception,
                "Failed to apply {Action} entry for party {PartyId} at {SettlementId}",
                action,
                partyId,
                settlementId);
            SendEntryRejection(
                peer,
                settlementId,
                action,
                "the server could not apply the request");
            return;
        }

        SendEntryResult(peer, settlementId, action, approved: true);
    }

    private bool TryValidateEntry(
        MobileParty party,
        Settlement settlement,
        SiegeEntryAction action,
        out string rejectionReason)
    {
        rejectionReason = null;

        if (!party.IsActive || party.Party == null)
            rejectionReason = "your party is inactive";
        else if (settlement.Party == null || !settlement.IsFortification)
            rejectionReason = "the target is not a fortification";
        else if (party.MapEvent != null)
            rejectionReason = "your party is already in a map event";
        else if (party.CurrentSettlement != null && party.CurrentSettlement != settlement)
            rejectionReason = "your party is inside another settlement";
        else if (party.BesiegerCamp != null)
            rejectionReason = "your party is already in another siege camp";
        else if (!distanceValidator.TryValidate(party, settlement, out var distanceRejectionReason))
            rejectionReason = distanceRejectionReason;
        else if ((party.ActualClan != null && party.ActualClan == settlement.OwnerClan) ||
            (party.MapFaction != null && party.MapFaction == settlement.MapFaction))
            rejectionReason = "your party belongs to the defending faction";
        else if (party.MapFaction == null ||
            settlement.MapFaction == null ||
            !FactionManager.IsAtWarAgainstFaction(party.MapFaction, settlement.MapFaction))
            rejectionReason = "your party is not at war with the settlement";
        else if (action == SiegeEntryAction.Besiege &&
            (settlement.SiegeEvent != null || party.Party.NumberOfHealthyMembers <= 0))
            rejectionReason = "your party cannot begin this siege";
        else if (action == SiegeEntryAction.Join &&
            (settlement.SiegeEvent == null ||
            !settlement.SiegeEvent.CanPartyJoinSide(party.Party, BattleSideEnum.Attacker)))
            rejectionReason = "your party cannot join the attacking side";

        return rejectionReason == null;
    }

    private void RejectEntry(
        NetPeer peer,
        string partyId,
        string settlementId,
        SiegeEntryAction action,
        string reason)
    {
        Logger.Warning(
            "Rejected {Action} entry for party {PartyId} at {SettlementId}: {Reason}",
            action,
            partyId,
            settlementId,
            reason);
        SendEntryRejection(peer, settlementId, action, reason);
    }

    private void SendEntryRejection(
        NetPeer peer,
        string settlementId,
        SiegeEntryAction action,
        string reason)
    {
        SendEntryResult(peer, settlementId, action, approved: false);

        var actionText = action == SiegeEntryAction.Besiege
            ? "begin the siege"
            : "join the siege";
        network.Send(
            peer,
            new SendInformationMessage($"Unable to {actionText}: {reason}."));
    }

    private void SendEntryResult(
        NetPeer peer,
        string settlementId,
        SiegeEntryAction action,
        bool approved)
    {
        if (action == SiegeEntryAction.Besiege)
        {
            network.Send(peer, new NetworkBesiegeSettlementApproved(approved));
            return;
        }

        network.Send(peer, new NetworkJoinSiegeCampApproved(settlementId, approved));
    }

    private enum SiegeEntryAction
    {
        Besiege,
        Join,
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
                    network.Send(peer, new NetworkBreakSiegeApproved(true, true, obj.FinishLocalMenus));
                    return;
                }

                Logger.Error("Party {PartyId} tried to leave a siege camp it is not in", obj.PartyId);
                network.Send(peer, new NetworkBreakSiegeApproved(false, false, obj.FinishLocalMenus));
                return;
            }

            siegeEventInterface.BreakSiege(party);

            network.Send(peer, new NetworkBreakSiegeApproved(true, false, obj.FinishLocalMenus));
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
        messageBroker.Unsubscribe<NetworkRequestBreakInContinuation>(HandleBreakInContinuation);
    }
}
