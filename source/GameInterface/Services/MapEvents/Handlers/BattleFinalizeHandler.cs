using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Logging;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.MobileParties.Data;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Settlements.Interfaces;
using GameInterface.Services.SiegeEvents.Patches;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Handlers;

/// <summary>
/// Owns finalizing a map event and tearing its encounter down (split out of <see cref="BattleHandler"/>). The
/// server finalizes on an explicit leave (<see cref="NetworkMapEventFinalizeAttempted"/>) and automatically on a
/// concluded victory (<see cref="MapEventConcluded"/>), deduping so <c>FinalizeEventAux</c> never runs twice, and
/// tells every involved player to close its encounter (<see cref="NetworkClosePvpEncounter"/>). The requester-only
/// <see cref="NetworkMapEventFinalized"/> reply remains as the no-player-party fallback.
/// </summary>
internal class BattleFinalizeHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleFinalizeHandler>();

    // Server-side: map events whose finalize has already run, so a duplicate finalize is ignored. A battle can
    // be finalized twice: the host leaves (finalize #1), host migration promotes another player, and that new
    // host's own "done" sends finalize #2. Re-running MapEvent.FinalizeEventAux re-forfeits the rosters (the same
    // troop removed twice -> the client roster goes negative). Keyed by the event INSTANCE via a weak table, so
    // it self-evicts when the event is GC'd (no growth, no eviction race vs. a duplicate that arrives a second
    // later) and never conflates two distinct events that happen to share an object id.
    private static readonly object FinalizedMarker = new object();
    private readonly ConditionalWeakTable<MapEvent, object> finalizedMapEvents = new ConditionalWeakTable<MapEvent, object>();
    private readonly object finalizedMapEventsLock = new object();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IMapEventLogger mapEventLogger;
    private readonly IBattleTroopReserveBuilder reserveBuilder;
    private readonly ISettlementInterface settlementInterface;
    private readonly IBattleHostRegistry hostRegistry;
    private readonly IPlayerManager playerManager;

    public BattleFinalizeHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IMapEventLogger mapEventLogger,
        IBattleTroopReserveBuilder reserveBuilder,
        ISettlementInterface settlementInterface,
        IBattleHostRegistry hostRegistry,
        IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.mapEventLogger = mapEventLogger;
        this.reserveBuilder = reserveBuilder;
        this.settlementInterface = settlementInterface;
        this.hostRegistry = hostRegistry;
        this.playerManager = playerManager;

        messageBroker.Subscribe<MapEventFinalizeAttempted>(Handle_MapEventFinalizeAttempted);
        messageBroker.Subscribe<NetworkMapEventFinalizeAttempted>(Handle_NetworkMapEventFinalizeAttempted);
        messageBroker.Subscribe<NetworkMapEventFinalized>(Handle_NetworkMapEventFinalized);
        messageBroker.Subscribe<NetworkRaidBattleResetToVillage>(Handle_NetworkRaidBattleResetToVillage);
        messageBroker.Subscribe<MapEventConcluded>(Handle_MapEventConcluded);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MapEventFinalizeAttempted>(Handle_MapEventFinalizeAttempted);
        messageBroker.Unsubscribe<NetworkMapEventFinalizeAttempted>(Handle_NetworkMapEventFinalizeAttempted);
        messageBroker.Unsubscribe<NetworkMapEventFinalized>(Handle_NetworkMapEventFinalized);
        messageBroker.Unsubscribe<NetworkRaidBattleResetToVillage>(Handle_NetworkRaidBattleResetToVillage);
        messageBroker.Unsubscribe<MapEventConcluded>(Handle_MapEventConcluded);
    }

    private void Handle_MapEventFinalizeAttempted(MessagePayload<MapEventFinalizeAttempted> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.MapEvent, out string mapEventId))
            return;

        if (MapEventConfig.Debug)
            mapEventLogger.DebugMapEvent(payload.What.MapEvent, "Map event finalize attempted. Sending network message to finalize map event on all clients.");

        var message = new NetworkMapEventFinalizeAttempted(mapEventId);
        if (ModInformation.IsServer)
        {
            Handle_NetworkMapEventFinalizeAttempted(new MessagePayload<NetworkMapEventFinalizeAttempted>(payload.Who, message));
            return;
        }

        network.SendAll(message);
    }

    private void Handle_NetworkMapEventFinalizeAttempted(MessagePayload<NetworkMapEventFinalizeAttempted> payload)
    {
        var requester = payload.Who as NetPeer;

        // Only the elected battle host may finalize a live shared battle: a client whose local mission
        // concluded early still runs vanilla's FinalizeEvent back on the map, and applying that here
        // would tear the battle down under everyone else (forced encounter close mid-mission). No reply
        // on a refusal — the finalized reply would pull the refused client off the menu vanilla parked
        // it on. Server-local publishes (requester null) and battles with no elected host pass.
        if (requester != null && hostRegistry.TryGet(payload.What.MapEventId, out var hostAssignment)
            && playerManager.TryGetPlayer(requester, out var requestingPlayer)
            && requestingPlayer.ControllerId != hostAssignment.HostControllerId)
        {
            Logger.Information("Refused finalize of {MapEventId} from non-host {ControllerId}",
                payload.What.MapEventId, requestingPlayer.ControllerId);
            return;
        }

        if (!objectManager.TryGetObjectWithLogging(payload.What.MapEventId, out MapEvent mapEvent))
        {
            if (requester != null)
                network.Send(requester, new NetworkMapEventFinalized());
            else
                messageBroker.Publish(this, new NetworkMapEventFinalized());

            return;
        }

        if (MapEventConfig.Debug)
            mapEventLogger.DebugMapEvent(mapEvent, "Handling network map event finalize attempted. Finalizing map event.");

        if (TryFinalizeRaidDefenderVictoryToVillage(mapEvent))
            return;

        var playerPartyIds = FinalizeAndCollectPlayers(mapEvent);

        // Tell every involved player party to close its encounter menu through the same path. The legacy
        // requester-only finalized reply tears down a different local path and can leave a stale encounter menu
        // in live p2p hostile battles when player-party collection races teardown.
        if (playerPartyIds.Length > 0)
        {
            PvpEncounterCloseSender.Send(network, messageBroker, this, playerPartyIds, mapEventId: payload.What.MapEventId);
            return;
        }

        network.Send(requester, new NetworkMapEventFinalized());
    }

    /// <summary>
    /// [Server] A battle reached a victory state — finalize it and close EVERY involved player's encounter, so a
    /// concluded coop battle tears down without the player leaving the post-battle menu (the auto-finalize on
    /// conclusion). There is no single leaver here, so the close instruction covers all involved players directly.
    /// </summary>
    private void Handle_MapEventConcluded(MessagePayload<MapEventConcluded> payload)
    {
        if (ModInformation.IsClient) return;

        var knownPlayerPartyIds = MapEventPlayerPartyCollector.Combine(payload.What.PlayerPartyIds);
        var closeAlreadySent = !string.IsNullOrEmpty(payload.What.SurrenderedPartyId);
        if (!objectManager.TryGetObjectWithLogging(payload.What.MapEventId, out MapEvent mapEvent))
        {
            if (!closeAlreadySent && knownPlayerPartyIds.Length > 0)
                PvpEncounterCloseSender.Send(network, messageBroker, this, knownPlayerPartyIds, payload.What.SurrenderedPartyId, payload.What.MapEventId);

            return;
        }

        if (MapEventConfig.Debug)
            mapEventLogger.DebugMapEvent(mapEvent, "Battle concluded; auto-finalizing and closing every involved player's encounter.");

        // Fires automatically on every victory, so guard the game thread: a finalize edge case must not escape
        // and tear down the campaign tick.
        try
        {
            var playerPartyIds = FinalizeAndCollectPlayers(mapEvent, knownPlayerPartyIds);

            if (!closeAlreadySent && playerPartyIds.Length > 0)
                PvpEncounterCloseSender.Send(network, messageBroker, this, playerPartyIds, payload.What.SurrenderedPartyId, payload.What.MapEventId);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to auto-finalize concluded map event");
        }
    }

    /// <summary>
    /// [Server] Finalize <paramref name="mapEvent"/> on the game thread, capturing the involved player party ids
    /// first (finalize clears them) so they get a reliable server-addressed encounter close instead of each
    /// racing its own local teardown. <see cref="GameThread.Run"/> runs inline when already on the game thread.
    /// </summary>
    private string[] FinalizeAndCollectPlayers(MapEvent mapEvent, string[] knownPlayerPartyIds = null)
    {
        if (!TryMarkFinalized(mapEvent))
            return MapEventPlayerPartyCollector.Combine(knownPlayerPartyIds);

        string[] playerPartyIds = null;
        GameThread.RunSafe(() =>
        {
            playerPartyIds = MapEventPlayerPartyCollector.Combine(
                knownPlayerPartyIds,
                MapEventPlayerPartyCollector.CollectPartyIds(mapEvent, objectManager));

            var excludedIds = CollectExcludedPlayerPartyIds(mapEvent);
            if (excludedIds.Count > 0)
                playerPartyIds = playerPartyIds.Where(id => !excludedIds.Contains(id)).ToArray();
            var raidSettlement = GetRaidFinalizationSettlement(mapEvent);
            var raidAttackers = GetRaidAttackerPlayerParties(mapEvent);

            // A winning inside defender is kept off the close above, but nothing seats it on the siege-defeated
            // menu: the server tears the SiegeEvent/MapEvent down via replication, bypassing vanilla's local
            // siege-end routing, so the winner falls through to the settlement arrival menu. Capture its parties
            // + settlement now (finalize clears them) and prompt after finalize (below), behind the event destroy.
            string defenderVictorySettlementId = null;
            string[] defenderVictoryPartyIds = null;
            if (mapEvent.IsSiegeAssault && mapEvent.BattleState == BattleState.DefenderVictory)
            {
                defenderVictoryPartyIds = CollectWinningInsideDefenderPartyIds(mapEvent);
                if (defenderVictoryPartyIds.Length > 0)
                    objectManager.TryGetId(mapEvent.MapEventSettlement, out defenderVictorySettlementId);
            }

            // The battle is over — drop its server-side troop reserves (ledger entry + flatten cache) so they
            // don't leak per battle. Done before FinalizeEventAux clears the parties, so the flatten-cache
            // cleanup can still enumerate them. No-op on a client (its ledger is never populated).
            reserveBuilder.ForgetMapEvent(mapEvent);

            // A siege assault that ends without a victor (attackers retreated or abandoned the fight)
            // keeps the siege in vanilla; a bare finalize would lift it. Victories finalize normally:
            // attacker victory captures the settlement, defender victory breaks the siege.
            if (mapEvent.IsSiegeAssault
                && mapEvent.BattleState != BattleState.AttackerVictory
                && mapEvent.BattleState != BattleState.DefenderVictory)
            {
                mapEvent._keepSiegeEvent = true;
                mapEvent.AttackerSide?.LeaderParty?.MobileParty?.RecalculateShortTermBehavior();
            }

            // Vanilla silently re-crowns AttackerSide.LeaderParty to whichever party is first in the
            // list if the leader's party ever left and rejoined the event; capture and the aftermath
            // prompt key on it, so re-assert the besieger camp leader before finalizing.
            if (mapEvent.IsSiegeAssault && mapEvent.AttackerSide != null)
            {
                var campLeader = mapEvent.MapEventSettlement?.SiegeEvent?.BesiegerCamp?.LeaderParty?.Party;
                if (campLeader != null && mapEvent.AttackerSide.LeaderParty != campLeader)
                {
                    mapEvent.AttackerSide.LeaderParty = campLeader;
                }
            }

            mapEvent.FinalizeEventAux();
            MoveRaidAttackersToSettlementGate(raidAttackers, raidSettlement);

            // After the destroy (same game thread, so behind it on the reliable-ordered channel).
            if (!string.IsNullOrEmpty(defenderVictorySettlementId))
                network.SendAll(new NetworkPromptSiegeDefenderVictory(defenderVictorySettlementId, defenderVictoryPartyIds));
        }, blocking: true, context: nameof(FinalizeAndCollectPlayers));
        return playerPartyIds ?? Array.Empty<string>();
    }

    private bool TryMarkFinalized(MapEvent mapEvent)
    {
        lock (finalizedMapEventsLock)
        {
            if (finalizedMapEvents.TryGetValue(mapEvent, out _))
            {
                objectManager.TryGetId(mapEvent, out var duplicateId);
                Logger.Warning("Ignoring duplicate finalize for already-finalized map event {MapEventId} (likely a post-migration second leave); not re-running the capture/roster forfeit.", duplicateId);
                return false;
            }
            finalizedMapEvents.Add(mapEvent, FinalizedMarker);
        }

        // The battle is over - release the mode claim so a later, unrelated battle on this event starts unclaimed.
        if (objectManager.TryGetId(mapEvent, out var mapEventIdForRelease))
            ServerBattleModeArbiter.Release(mapEventIdForRelease);

        return true;
    }

    // [Server, game thread] Player parties that must keep their encounter through the finalize:
    // a capturing leader enters the settlement-taken flow, and a winning inside defender sits on
    // its vanilla victory menu — the close's Finish + ExitToLast would tear either down.
    private HashSet<string> CollectExcludedPlayerPartyIds(MapEvent mapEvent)
    {
        var excluded = new HashSet<string>();
        if (mapEvent?.AttackerSide == null || mapEvent.DefenderSide == null) return excluded;

        SiegeAftermathPatches.TryGetPlayerCaptureLeader(mapEvent, out var capturingLeader, out _);

        foreach (var party in mapEvent.InvolvedParties)
        {
            bool isCapturingLeader = capturingLeader != null && party?.MobileParty == capturingLeader;
            bool isWinningInsideDefender = mapEvent.IsSiegeAssault
                && mapEvent.BattleState == BattleState.DefenderVictory
                && party?.Side == BattleSideEnum.Defender
                && party.MobileParty?.CurrentSettlement == mapEvent.MapEventSettlement;
            if (!isCapturingLeader && !isWinningInsideDefender) continue;

            if (objectManager.TryGetId(party, out var id)) excluded.Add(id);
        }

        return excluded;
    }

    // [Server, game thread] The winning inside-defender player parties, for the siege-defeated-menu prompt.
    private string[] CollectWinningInsideDefenderPartyIds(MapEvent mapEvent)
    {
        List<string> ids = null;
        foreach (var party in mapEvent.InvolvedParties)
        {
            if (party?.Side != BattleSideEnum.Defender) continue;
            if (party.MobileParty?.CurrentSettlement != mapEvent.MapEventSettlement) continue;
            if (!objectManager.TryGetId(party, out var id)) continue;

            if (ids == null) ids = new List<string>();
            ids.Add(id);
        }

        return ids?.ToArray() ?? Array.Empty<string>();
    }
    private bool TryFinalizeRaidDefenderVictoryToVillage(MapEvent mapEvent)
    {
        var shouldReset = false;
        string[] playerPartyIds = null;
        string settlementId = null;

        GameThread.RunSafe(
            () =>
            {
                if (!ShouldResetRaidDefenderVictoryToVillage(mapEvent))
                    return;

                var settlement = mapEvent.MapEventSettlement;
                if (!objectManager.TryGetIdWithLogging(settlement, out settlementId))
                    return;

                if (!TryMarkFinalized(mapEvent))
                    return;

                var involvedParties = CollectInvolvedParties(mapEvent);
                playerPartyIds = MapEventPlayerPartyCollector.CollectPartyIds(mapEvent, objectManager);

                reserveBuilder.ForgetMapEvent(mapEvent);
                mapEvent.FinalizeEventAux();
                ClearMapEventBackReferences(involvedParties);
                ResetRaidSettlementState(settlement);
                ReturnPlayerPartiesToSettlement(playerPartyIds, settlement);
                shouldReset = true;
            },
            blocking: true,
            context: nameof(TryFinalizeRaidDefenderVictoryToVillage));

        if (!shouldReset)
            return false;

        network.SendAll(new NetworkRaidBattleResetToVillage(playerPartyIds, settlementId));
        return true;
    }

    private void Handle_NetworkRaidBattleResetToVillage(MessagePayload<NetworkRaidBattleResetToVillage> payload)
    {
        if (ModInformation.IsServer) return;

        var message = payload.What;

        GameThread.RunSafe(
            () =>
            {
                if (Campaign.Current == null) return;
                if (!objectManager.TryGetId(MobileParty.MainParty?.Party, out var myPartyId)) return;
                if (message.PartyIds == null || message.PartyIds.Length == 0) return;
                if (Array.IndexOf(message.PartyIds, myPartyId) < 0) return;
                if (!objectManager.TryGetObjectWithLogging<Settlement>(message.SettlementId, out var settlement)) return;

                BattleModeRegistry.End();
                ResetLocalRaidBattleToVillage(settlement);
            },
            context: nameof(Handle_NetworkRaidBattleResetToVillage));
    }

    private void ResetLocalRaidBattleToVillage(Settlement settlement)
    {
        var mainParty = MobileParty.MainParty;
        if (mainParty == null)
            return;

        using (new AllowedThread())
        {
            mainParty.Party._mapEventSide = null;

            if (PlayerEncounter.Current != null)
                PlayerEncounter.Finish(false);

            if (mainParty.CurrentSettlement != settlement)
                settlementInterface.PartyEnterSettlement(mainParty, settlement);

            ResetRaidSettlementState(settlement);
            settlementInterface.StartSettlementEncounter(mainParty, settlement);
        }

        mainParty.SetMoveModeHold();
        GameMenu.SwitchToMenu("village");
    }

    private void ReturnPlayerPartiesToSettlement(string[] playerPartyIds, Settlement settlement)
    {
        foreach (var playerPartyId in playerPartyIds ?? Array.Empty<string>())
        {
            if (!objectManager.TryGetObject<PartyBase>(playerPartyId, out var party))
                continue;

            var mobileParty = party.MobileParty;
            if (mobileParty == null || mobileParty.CurrentSettlement == settlement)
                continue;

            settlementInterface.PartyEnterSettlement(mobileParty, settlement);
        }
    }

    private static PartyBase[] CollectInvolvedParties(MapEvent mapEvent)
    {
        if (mapEvent?.AttackerSide == null || mapEvent.DefenderSide == null)
            return Array.Empty<PartyBase>();

        return mapEvent.InvolvedParties.Where(party => party != null).ToArray();
    }

    private static void ClearMapEventBackReferences(PartyBase[] parties)
    {
        foreach (var party in parties)
        {
            if (party?.MapEventSide != null)
                party._mapEventSide = null;
        }
    }

    private static bool ShouldResetRaidDefenderVictoryToVillage(MapEvent mapEvent)
    {
        if (!mapEvent.IsRaidHostileAction())
            return false;

        if (!IsAttackerVictory(mapEvent))
            return false;

        var settlement = mapEvent.MapEventSettlement;
        if (settlement?.Village == null)
            return false;

        if (settlement.SettlementHitPoints <= 0f || settlement.Village.VillageState == Village.VillageStates.Looted)
            return false;

        return true;
    }

    private static void ResetRaidSettlementState(Settlement settlement)
    {
        if (settlement?.Village == null)
            return;

        settlement.Village.VillageState = Village.VillageStates.Normal;
        if (settlement.SettlementHitPoints < 1f)
            settlement.SettlementHitPoints = 1f;
    }

    private static bool IsAttackerVictory(MapEvent mapEvent)
    {
        return mapEvent.WinningSide == BattleSideEnum.Attacker ||
               mapEvent.BattleState == BattleState.AttackerVictory;
    }

    private void Handle_NetworkMapEventFinalized(MessagePayload<NetworkMapEventFinalized> payload)
    {
        GameThread.RunSafe(() =>
        {
            if (Campaign.Current == null) return;

            // The local player's battle has ended — clear the recorded mode so the encounter-menu gate
            // (BattleModeEncounterOptionsPatch) no longer treats this event as claimed.
            BattleModeRegistry.End();

            // When this battle ended with the local player captured, the captivity flow owns the UI:
            // PlayerCaptivityClientHandler has switched to the prisoner menu and leaves the encounter
            // itself. Exiting menus here would close the capture screen.
            if (PlayerCaptivity.IsCaptive) return;

            var mainParty = MobileParty.MainParty;
            MoveLocalRaidPartyToSettlementGate(mainParty, GetLocalRaidFinalizationSettlement(mainParty));

            if (PlayerEncounter.Current != null)
            {
                // TODO determine force out of settlement
                PlayerEncounter.Finish(true);
            }

            GameMenu.ExitToLast();
        });
    }

    private void MoveRaidAttackersToSettlementGate(MobileParty[] raidAttackers, Settlement settlement)
    {
        if (settlement?.Village == null)
            return;

        foreach (var mobileParty in raidAttackers)
        {
            if (mobileParty == null)
                continue;

            mobileParty.Position = settlement.GatePosition;
            if (mobileParty.CurrentSettlement == settlement)
                settlementInterface.PartyLeaveSettlement(mobileParty);

            using (new AllowedThread())
            {
                mobileParty.SetMoveModeHold();
            }
            mobileParty.ResetNavigationToHold();
            PublishPartyBehaviorUpdate(mobileParty);
        }
    }

    private void MoveLocalRaidPartyToSettlementGate(MobileParty mainParty, Settlement settlement)
    {
        if (mainParty == null || settlement?.Village == null)
            return;

        using (new AllowedThread())
        {
            mainParty.Position = settlement.GatePosition;
            if (mainParty.CurrentSettlement == settlement)
                settlementInterface.PartyLeaveSettlement(mainParty);

            mainParty.ResetNavigationToHold();
        }
    }

    private void PublishPartyBehaviorUpdate(MobileParty mobileParty)
    {
        if (!objectManager.TryGetId(mobileParty, out var mobilePartyId))
            return;

        var data = new PartyBehaviorUpdateData(
            mobilePartyId,
            mobileParty.DefaultBehavior,
            null,
            mobileParty.Position,
            false,
            mobileParty.Position,
            mobileParty.DefaultBehavior,
            mobileParty.TargetPosition,
            mobileParty.DesiredAiNavigationType)
        {
            ForcePosition = true,
        };

        // The gate reset must reach clients before the encounter close allows another map command.
        messageBroker.Publish(this, new PartyBehaviorUpdated(ref data));
    }

    private static MobileParty[] GetRaidAttackerPlayerParties(MapEvent mapEvent)
    {
        if (mapEvent?.IsRaidHostileAction() != true || mapEvent.AttackerSide == null)
            return Array.Empty<MobileParty>();

        return mapEvent.AttackerSide.Parties
            .Select(mapEventParty => mapEventParty.Party?.MobileParty)
            .Where(mobileParty => mobileParty?.IsPlayerParty() == true)
            .ToArray();
    }

    private static Settlement GetLocalRaidFinalizationSettlement(MobileParty mainParty)
    {
        var settlement = GetRaidFinalizationSettlement(PlayerEncounter.Battle ?? mainParty?.MapEvent);
        if (settlement != null)
            return settlement;

        if (PlayerEncounter.Current?.ForceRaid == true && mainParty?.CurrentSettlement?.Village != null)
            return mainParty.CurrentSettlement;

        return null;
    }

    private static Settlement GetRaidFinalizationSettlement(MapEvent mapEvent)
    {
        if (mapEvent?.IsRaidHostileAction() != true)
            return null;

        if (mapEvent.MapEventSettlement?.Village != null)
            return mapEvent.MapEventSettlement;

        if (mapEvent.DefenderSide?.LeaderParty?.Settlement?.Village != null)
            return mapEvent.DefenderSide.LeaderParty.Settlement;

        if (mapEvent.DefenderSide == null)
            return null;

        foreach (var mapEventParty in mapEvent.DefenderSide.Parties)
        {
            if (mapEventParty.Party?.Settlement?.Village != null)
                return mapEventParty.Party.Settlement;
        }

        return null;
    }
}
