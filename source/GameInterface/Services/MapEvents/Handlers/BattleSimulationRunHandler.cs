using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Common.Util;
using GameInterface.Services.MapEvents.Logging;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.MapEventSides.Messages;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Handlers;

/// <summary>
/// Runs auto-resolve battle simulations authoritatively on the server, paced by the requesting client.
/// </summary>
/// <remarks>
/// Client: the send-troops consequence prefix asks <c>BattleStartCoordinator</c> to start the auto-resolve, which
/// blocks on the server's accept before the scoreboard opens;
/// the local engine is disabled and <see cref="BattleSimulationReplay"/> instead drives the playback off
/// <c>_numTicks</c>, emitting <see cref="RequestAdvanceBattleSimulation"/> each round.
/// Server: on the request it only sets the simulation up; each advance resolves that many rounds, syncing
/// casualties (via the TroopRoster patches) and <c>BattleState</c> as it goes and streaming the round's
/// scoreboard updates back as <see cref="NetworkBattleSimulationRound"/>. When the battle is decided it
/// finalizes and sends <see cref="NetworkBattleSimulationFinished"/>.
/// Client: replays each round onto the scoreboard and finishes once playback drains.
/// </remarks>
internal class BattleSimulationRunHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleSimulationRunHandler>();

    // Safety bound so a non-terminating simulation can never hang the server thread.
    private const int MaxSimulationRounds = 10000;

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IMapEventLogger mapEventLogger;

    private sealed class ActiveSimulation
    {
        public NetPeer Peer;
        public MapEvent MapEvent;
        public ForwardingBattleObserver Observer;
        public IBattleObserver PreviousObserver;
    }

    private readonly Dictionary<string, ActiveSimulation> activeSimulations = new();
    private readonly object simLock = new();

    public BattleSimulationRunHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IMapEventLogger mapEventLogger)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.mapEventLogger = mapEventLogger;

        messageBroker.Subscribe<RequestAdvanceBattleSimulation>(Handle_RequestAdvanceBattleSimulation);
        messageBroker.Subscribe<NetworkBattleStartRequest>(Handle_NetworkBattleStartRequest);
        messageBroker.Subscribe<NetworkAdvanceBattleSimulation>(Handle_NetworkAdvanceBattleSimulation);
        messageBroker.Subscribe<NetworkBattleSimulationRound>(Handle_NetworkBattleSimulationRound);
        messageBroker.Subscribe<NetworkBattleSimulationLoot>(Handle_NetworkBattleSimulationLoot);
        messageBroker.Subscribe<NetworkBattleSimulationFinished>(Handle_NetworkBattleSimulationFinished);
        messageBroker.Subscribe<NetworkOpenBattleSimulation>(Handle_NetworkOpenBattleSimulation);
        messageBroker.Subscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
        messageBroker.Subscribe<MapEventPartyBattlePartyAdded>(Handle_MapEventPartyBattlePartyAdded);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<RequestAdvanceBattleSimulation>(Handle_RequestAdvanceBattleSimulation);
        messageBroker.Unsubscribe<NetworkBattleStartRequest>(Handle_NetworkBattleStartRequest);
        messageBroker.Unsubscribe<NetworkAdvanceBattleSimulation>(Handle_NetworkAdvanceBattleSimulation);
        messageBroker.Unsubscribe<NetworkBattleSimulationRound>(Handle_NetworkBattleSimulationRound);
        messageBroker.Unsubscribe<NetworkBattleSimulationLoot>(Handle_NetworkBattleSimulationLoot);
        messageBroker.Unsubscribe<NetworkBattleSimulationFinished>(Handle_NetworkBattleSimulationFinished);
        messageBroker.Unsubscribe<NetworkOpenBattleSimulation>(Handle_NetworkOpenBattleSimulation);
        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
        messageBroker.Unsubscribe<MapEventPartyBattlePartyAdded>(Handle_MapEventPartyBattlePartyAdded);
    }

    /// <summary>[Client] Forward a playback-paced advance to the server.</summary>
    private void Handle_RequestAdvanceBattleSimulation(MessagePayload<RequestAdvanceBattleSimulation> payload)
    {
        network.SendAll(new NetworkAdvanceBattleSimulation(payload.What.MapEventId, payload.What.Rounds));
    }

    /// <summary>[Server] Handle a battle-start request for the auto-resolve mode: gate it, set the simulation up
    /// (no rounds yet; the client paces it via advances), and reply. Requests for other modes are ignored here.</summary>
    private void Handle_NetworkBattleStartRequest(MessagePayload<NetworkBattleStartRequest> payload)
    {
        if (ModInformation.IsClient)
            return;

        if (payload.What.Mode != (int)BattleStartMode.Simulation)
            return;

        if (!(payload.Who is NetPeer requestingPeer))
        {
            Logger.Error("Received {Message} with no originating peer", nameof(NetworkBattleStartRequest));
            return;
        }

        var mapEventId = payload.What.MapEventId;

        if (!objectManager.TryGetObjectWithLogging<MapEvent>(mapEventId, out var mapEvent))
            return;

        if (mapEvent.HasWinner)
        {
            mapEventLogger.DebugMapEvent(mapEvent, "Battle simulation requested for an already finished map event; rejecting");
            network.Send(requestingPeer, new NetworkBattleStartReply(payload.What.RequestId, false));
            return;
        }

        // Server-authoritative mode gate: accept the auto-resolve only if no live mission already owns this event.
        // On reject the requesting client never opened its scoreboard (the prefix deferred it), so there is nothing
        // to tear down — the request is simply dropped.
        if (!ServerBattleModeArbiter.TryClaimSimulation(mapEventId))
        {
            mapEventLogger.DebugMapEvent(mapEvent, "Rejecting battle simulation: a live mission is already underway for this event");
            network.Send(requestingPeer, new NetworkBattleStartReply(payload.What.RequestId, false));
            return;
        }

        // Guard against a double-start: two clients can both click auto-resolve for the same event inside the
        // broadcast-latency window, and TryClaimSimulation lets the second through — it only rejects the OTHER
        // mode, so an already-simulation claim still succeeds. Without this the second request would set the
        // simulation up again (overwriting the first's activeSimulations entry, orphaning its observer) and its
        // requester would also become a pacer. Reject the duplicate so the first stays the sole pacer; the
        // arbiter claim is left intact (the first still owns it). Reliable on the single network thread: the
        // first request only returns after its blocking GameThread.Run below has populated activeSimulations.
        lock (simLock)
        {
            if (activeSimulations.ContainsKey(mapEventId))
            {
                mapEventLogger.DebugMapEvent(mapEvent, "Battle simulation already active for this event; rejecting duplicate start");
                network.Send(requestingPeer, new NetworkBattleStartReply(payload.What.RequestId, false));
                return;
            }
        }

        var observer = new ForwardingBattleObserver(objectManager);

        GameThread.Run(() =>
        {
            // v1: simulate the full participating troop count (null), not the player's selected subset.
            // Set up before attaching the observer: setup fires +1 TroopNumberChanged calls to populate the
            // scoreboard, which the client already does for itself and must not receive twice.
            var previousObserver = mapEvent.BattleObserver;
            mapEvent.SimulateBattleSetup(null);
            mapEvent.BattleObserver = observer;

            lock (simLock)
            {
                activeSimulations[mapEventId] = new ActiveSimulation
                {
                    Peer = requestingPeer,
                    MapEvent = mapEvent,
                    Observer = observer,
                    PreviousObserver = previousObserver,
                };
            }
        }, blocking: true);

        // Mirror the simulation onto every other client in this map event. Each client opens the window only if its
        // own party is in the event; the requesting client and uninvolved clients ignore it. The requester keeps
        // pacing; spectators replay passively.
        // Claim the event for the simulation mode on every client (greys the mission option for anyone at the menu),
        // then open the spectator scoreboards and accept the requester.
        network.SendAll(new NetworkBattleModeSet(mapEventId, (int)BattleStartMode.Simulation));
        network.SendAllBut(requestingPeer, new NetworkOpenBattleSimulation(mapEventId));
        network.Send(requestingPeer, new NetworkBattleStartReply(payload.What.RequestId, true));

        mapEventLogger.DebugMapEvent(mapEvent, "Battle simulation set up; awaiting client-paced advances");
    }

    /// <summary>
    /// [Server] A party was added to a side that has an active simulation (a player joining the battle, or an
    /// AI reinforcement). The simulation's troop pool was allocated once at setup and never re-reads the side's
    /// parties, so without this the joiner never fights and never appears on any scoreboard. Fold its troops
    /// into the live pool; the +1s fired here are captured by the attached observer and stream out with the
    /// next advance, so every client with the window open gets the new rows through the normal round pipeline.
    /// </summary>
    private void Handle_MapEventPartyBattlePartyAdded(MessagePayload<MapEventPartyBattlePartyAdded> payload)
    {
        if (ModInformation.IsClient)
            return;

        var side = payload.What.MapEventSide;
        var joiningParty = payload.What.MapEventParty;
        if (side?.MapEvent == null || joiningParty == null)
            return;

        // Publishing happens during the native AddPartyInternal, which already runs on the game thread; this
        // only touches the simulation pool via the party object, so it is safe here. GameThread.Run runs inline
        // when already on that thread.
        GameThread.Run(() => 
        {
            if (!objectManager.TryGetId(side.MapEvent, out var mapEventId))
                return;

            if (!activeSimulations.ContainsKey(mapEventId))
                return;

            AddPartyToActiveSimulation(side, joiningParty);
        });
    }

    /// <summary>[Server, main thread] Allocate a late-joining party's troops into the live simulation pool.</summary>
    private void AddPartyToActiveSimulation(MapEventSide side, MapEventParty joiningParty)
    {
        try
        {
            // A troop-limited battle (hideout, lord's hall) trims and locks its rosters at setup; vanilla
            // refuses to re-ready a locked side, so a late joiner cannot be folded in safely there.
            if (side._troopAllocationsLocked)
                return;

            if (side._simulationTroopList == null || side._allocatedTroops == null || side._readyTroopsPriorityList == null)
                return;

            var sizeOfParty = joiningParty.Party.NumberOfHealthyMembers;
            if (sizeOfParty <= 0)
                return;

            // Build the joiner's ready-troop entries and allocate them, mirroring MapEventSide.MakeReadyParty +
            // AllocateTroops but only for this one party so the existing parties' allocations stay untouched.
            int startIndex = side._readyTroopsPriorityList.Count;
            joiningParty.SetParticipatingTroopCount(sizeOfParty);
            joiningParty.Update();
            Campaign.Current.Models.TroopSupplierProbabilityModel
                .EnqueueTroopSpawnProbabilitiesAccordingToUnitSpawnPrioritization(
                    joiningParty, null, false, sizeOfParty, false, side._readyTroopsPriorityList);

            var observer = side.MapEvent.BattleObserver;
            int allocated = 0;
            for (int i = startIndex; i < side._readyTroopsPriorityList.Count; i++)
            {
                var (element, party, _) = side._readyTroopsPriorityList[i];
                var descriptor = element.Descriptor;
                if (side._allocatedTroops.ContainsKey(descriptor))
                    continue;

                side._simulationTroopList.Add(descriptor);
                side._allocatedTroops.Add(descriptor, party);
                observer?.TroopNumberChanged(side.MissionSide, party.Party, element.Troop, 1);
                allocated++;
            }

            side._readyTroopsPriorityList.RemoveRange(startIndex, side._readyTroopsPriorityList.Count - startIndex);

            mapEventLogger.DebugMapEvent(side.MapEvent,
                "Folded joining party {PartyId} into the active simulation ({TroopCount} troops)",
                joiningParty.Party.Id, allocated);
        }
        catch (Exception e)
        {
            Logger.Error(e, "Failed to add joining party to active battle simulation");
        }
    }

    /// <summary>[Server] Resolve the requested number of rounds, streaming each round back.</summary>
    private void Handle_NetworkAdvanceBattleSimulation(MessagePayload<NetworkAdvanceBattleSimulation> payload)
    {
        if (ModInformation.IsClient)
            return;

        var mapEventId = payload.What.MapEventId;

        ActiveSimulation sim;
        lock (simLock)
        {
            if (!activeSimulations.TryGetValue(mapEventId, out sim))
                return;
        }

        var maxRounds = payload.What.MaxRounds;
        var finished = false;
        NetworkBattleSimulationLoot lootMessage = default;
        var hasLoot = false;

        GameThread.Run(() =>
        {
            // Accumulate every round resolved in this advance into one update. Normal playback advances
            // a single round per call (one packet per round, as before), but a "skip" resolves the whole
            // remaining battle in one advance: batching keeps that from flooding the peer's outbound
            // queue with a packet per round.
            var batched = new List<BattleSimTroopChange>();

            int rounds = 0;
            while (rounds < maxRounds && rounds < MaxSimulationRounds && !sim.MapEvent.HasWinner)
            {
                rounds++;
                sim.MapEvent.SimulatePlayerEncounterBattle();

                var changes = sim.Observer.FlushRound();
                if (changes.Length > 0)
                    batched.AddRange(changes);
            }

            // Broadcast to everyone: the pacer and the spectators (clients in this event) all replay these rounds.
            // Clients not in the event ignore rounds for a map event they have no active playback for.
            if (batched.Count > 0)
                network.SendAll(new NetworkBattleSimulationRound(mapEventId, batched.ToArray()));

            if (sim.MapEvent.HasWinner)
            {
                // Capture the casualties and winner contributions before tearing the simulation down so the
                // winning client can re-run the native loot flow locally and open its loot screen.
                if (PlayerWonSimulation(sim.MapEvent))
                {
                    lootMessage = new NetworkBattleSimulationLoot(
                        mapEventId,
                        sim.MapEvent.BattleState,
                        CollectDefeatedCasualties(sim.MapEvent),
                        CollectWinnerContributions(sim.MapEvent));
                    hasLoot = true;
                }

                EndSimulationSession(sim);
                finished = true;
            }
        }, blocking: true);

        if (finished)
        {
            lock (simLock)
            {
                activeSimulations.Remove(mapEventId);
            }

            mapEventLogger.DebugMapEvent(sim.MapEvent, "Server-side battle simulation finished. BattleState={BattleState}", sim.MapEvent.BattleState);

            // Loot first so the client applies it before the finish closes the playback.
            if (hasLoot)
                network.Send(sim.Peer, lootMessage);

            network.SendAll(new NetworkBattleSimulationFinished(mapEventId));
        }
    }

    /// <summary>[Client] Resolve a streamed round and queue it for playback.</summary>
    private void Handle_NetworkBattleSimulationRound(MessagePayload<NetworkBattleSimulationRound> payload)
    {
        var message = payload.What;
        if (message.Changes == null || message.Changes.Length == 0)
            return;

        // Resolve and enqueue on the main thread: objectManager can be mutated by the main thread's
        // Add/Remove, and BattleSimulationReplay's round queue is drained on the main-thread tick.
        GameThread.Run(() =>
        {
            // Rounds are broadcast to everyone; only clients actually playing this simulation (the pacer and the
            // in-event spectators) replay them. Checked here (not on the network thread) so it observes the Begin
            // done by NetworkOpenBattleSimulation, which is queued onto the main thread before this.
            if (!BattleSimulationReplay.IsActiveFor(message.MapEventId))
                return;

            var resolved = new List<BattleSimulationReplay.ResolvedChange>(message.Changes.Length);
            foreach (var change in message.Changes)
            {
                if (!objectManager.TryGetObject<PartyBase>(change.PartyId, out var party))
                    continue;

                // A client builds its own party's troops from its local scoreboard setup when it opens, so it must
                // never re-add them from the stream. The only positive change for the local party is the server's
                // fold-in +1 fired when this client joined an in-progress simulation, which would double its count.
                if (party == PartyBase.MainParty && change.Number > 0)
                    continue;

                if (!TryResolveCharacterObject(change.CharacterId, change.IsHero, out var character))
                    continue;

                resolved.Add(new BattleSimulationReplay.ResolvedChange(
                    (BattleSideEnum)change.Side, party, character,
                    change.Number, change.NumberKilled, change.NumberWounded, change.NumberRouted, change.KillCount, change.NumberReadyToUpgrade));
            }

            if (resolved.Count > 0)
                BattleSimulationReplay.EnqueueRound(resolved.ToArray());
        });
    }

    /// <summary>[Server] True when a player party is on the winning side, so a client needs the loot screen.</summary>
    private static bool PlayerWonSimulation(MapEvent mapEvent)
    {
        if (mapEvent.WinningSide == BattleSideEnum.None)
            return false;

        return mapEvent.GetMapEventSide(mapEvent.WinningSide).Parties
            .Any(p => p.Party.MobileParty?.IsPlayerParty() == true);
    }

    /// <summary>[Server] Serialize each defeated party's simulation casualties for the client to replay.</summary>
    private BattleSimDefeatedParty[] CollectDefeatedCasualties(MapEvent mapEvent)
    {
        var parties = new List<BattleSimDefeatedParty>();

        foreach (var defeated in mapEvent.GetMapEventSide(mapEvent.DefeatedSide).Parties)
        {
            if (!objectManager.TryGetId(defeated.Party, out var partyId))
                continue;

            var died = SerializeCasualties(defeated.DiedInBattle);
            var wounded = SerializeCasualties(defeated.WoundedInBattle);
            if (died.Length == 0 && wounded.Length == 0)
                continue;

            parties.Add(new BattleSimDefeatedParty(partyId, died, wounded));
        }

        return parties.ToArray();
    }

    /// <summary>[Server] Serialize each winning party's battle contribution; the loot chance models need it &gt; 0.</summary>
    private BattleSimWinner[] CollectWinnerContributions(MapEvent mapEvent)
    {
        var winners = new List<BattleSimWinner>();

        foreach (var winner in mapEvent.GetMapEventSide(mapEvent.WinningSide).Parties)
        {
            if (!objectManager.TryGetId(winner.Party, out var partyId))
                continue;

            winners.Add(new BattleSimWinner(partyId, winner.ContributionToBattle));
        }

        return winners.ToArray();
    }

    private BattleSimCasualty[] SerializeCasualties(TroopRoster roster)
    {
        var casualties = new List<BattleSimCasualty>();

        foreach (var element in roster.GetTroopRoster())
        {
            var character = element.Character;
            if (character == null)
                continue;

            var isHero = character.IsHero;
            var objectToResolve = isHero ? (object)character.HeroObject : character;
            if (objectToResolve == null || !objectManager.TryGetId(objectToResolve, out var characterId))
                continue;

            casualties.Add(new BattleSimCasualty(characterId, isHero, element.Number, element.WoundedNumber));
        }

        return casualties.ToArray();
    }

    /// <summary>
    /// [Client] Replay the simulation casualties and apply the winning <c>BattleState</c> so the native
    /// PlayerEncounter result flow rolls the loot and opens the loot screen on the next tick.
    /// </summary>
    private void Handle_NetworkBattleSimulationLoot(MessagePayload<NetworkBattleSimulationLoot> payload)
    {
        var message = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!BattleSimulationReplay.IsActiveFor(message.MapEventId))
                return;

            if (!objectManager.TryGetObject<MapEvent>(message.MapEventId, out var mapEvent))
                return;

            // Re-applying server-authoritative results; the roster patches must stand down during the apply.
            using (new AllowedThread())
            {
                // The loot/capture chance models drop any winner with ContributionToBattle == 0, which is the
                // case on the client (its simulation engine never ran). Restore the server's values first.
                foreach (var winner in message.Winners ?? Array.Empty<BattleSimWinner>())
                {
                    if (!objectManager.TryGetObject<PartyBase>(winner.PartyId, out var winnerParty))
                        continue;

                    var winnerMapEventParty = FindMapEventParty(mapEvent, winnerParty);
                    if (winnerMapEventParty != null)
                        winnerMapEventParty._contributionToBattle = winner.ContributionToBattle;
                }

                foreach (var defeated in message.DefeatedParties ?? Array.Empty<BattleSimDefeatedParty>())
                {
                    if (!objectManager.TryGetObject<PartyBase>(defeated.PartyId, out var party))
                        continue;

                    var mapEventParty = FindMapEventParty(mapEvent, party);
                    if (mapEventParty == null)
                        continue;

                    ApplyCasualties(mapEventParty.DiedInBattle, defeated.Died);
                    ApplyCasualties(mapEventParty.WoundedInBattle, defeated.Wounded);
                }

                mapEvent.BattleState = message.WinningState;
            }
        });
    }

    private void ApplyCasualties(TroopRoster roster, BattleSimCasualty[] casualties)
    {
        if (casualties == null)
            return;

        foreach (var casualty in casualties)
        {
            if (!TryResolveCharacterObject(casualty.CharacterId, casualty.IsHero, out var character))
                continue;

            roster.AddToCounts(character, casualty.Number, insertAtFront: false, casualty.WoundedNumber);
        }
    }

    private static MapEventParty FindMapEventParty(MapEvent mapEvent, PartyBase party)
    {
        foreach (var side in mapEvent._sides)
            foreach (var mapEventParty in side.Parties)
                if (mapEventParty.Party == party)
                    return mapEventParty;

        return null;
    }

    /// <summary>[Client] Server finished simulating: end playback once the queued rounds drain.</summary>
    private void Handle_NetworkBattleSimulationFinished(MessagePayload<NetworkBattleSimulationFinished> payload)
    {
        // Both the encounter state and the replay's finish flag belong to the main-thread tick.
        GameThread.Run(() =>
        {
            // Broadcast to everyone; only a client actually playing this simulation finishes it.
            if (!BattleSimulationReplay.IsActiveFor(payload.What.MapEventId))
                return;

            if (PlayerEncounter.CurrentBattleSimulation == null)
            {
                Logger.Warning("Received {Message} but no battle simulation is active", nameof(NetworkBattleSimulationFinished));
                return;
            }

            BattleSimulationReplay.RequestFinish();
        });
    }

    /// <summary>
    /// [Client] Another player started an auto-resolve simulation for a map event. If this client's own party is in
    /// that event (and it isn't the initiator), open the same simulation window as a passive spectator so it can
    /// watch the server-streamed results. Clients not in the event ignore it.
    /// </summary>
    private void Handle_NetworkOpenBattleSimulation(MessagePayload<NetworkOpenBattleSimulation> payload)
    {
        if (ModInformation.IsServer)
            return;

        var mapEventId = payload.What.MapEventId;

        GameThread.Run(() =>
        {
            // The initiator already has it open and is pacing it — it opened synchronously when the server accepted
            // its blocking request, marking the replay active.
            if (BattleSimulationReplay.IsActiveFor(mapEventId))
                return;

            if (!objectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent) || mapEvent == null)
                return;

            // Only a player actually in this battle (in its encounter) spectates it.
            if (PlayerEncounter.Current == null || PlayerEncounter.Battle != mapEvent)
                return;

            if (PlayerEncounter.CurrentBattleSimulation != null)
                return;

            var mapState = Game.Current.GameStateManager.LastOrDefault<MapState>();
            if (mapState == null)
            {
                Logger.Warning("Cannot open spectator battle simulation: no active MapState");
                return;
            }

            // Begin (which marks the replay active) must precede StartBattleSimulation. InitSimulation(null, null)
            // builds the scoreboard from the event's full parties; the server-streamed rounds then drive it.
            BattleSimulationReplay.Begin(mapEventId, spectator: true);
            PlayerEncounter.InitSimulation(null, null);
            mapState.StartBattleSimulation();

            mapEventLogger.DebugMapEvent(mapEvent, "Opened spectator battle simulation window");
        });
    }

    /// <summary>
    /// [Server] The pacing client dropped: finish and tear down any simulations it was driving so the
    /// swapped-in observer is restored and the tracking entry doesn't leak.
    /// </summary>
    private void Handle_PlayerDisconnected(MessagePayload<PlayerDisconnected> payload)
    {
        if (ModInformation.IsClient)
            return;

        var peer = payload.What.PlayerId;

        List<KeyValuePair<string, ActiveSimulation>> orphaned;
        lock (simLock)
        {
            orphaned = activeSimulations.Where(entry => entry.Value.Peer == peer).ToList();
        }

        if (orphaned.Count == 0)
            return;

        GameThread.Run(() =>
        {
            foreach (var entry in orphaned)
            {
                var sim = entry.Value;

                // No client left to pace the playback, so resolve whatever remains and let the battle
                // reach its decision (casualties still sync to the other clients via the troop-roster
                // patches) instead of leaving the map event half-simulated.
                int rounds = 0;
                while (rounds < MaxSimulationRounds && !sim.MapEvent.HasWinner)
                {
                    rounds++;
                    sim.MapEvent.SimulatePlayerEncounterBattle();
                }

                EndSimulationSession(sim);
                mapEventLogger.DebugMapEvent(sim.MapEvent, "Battle simulation client disconnected; finished server-side. BattleState={BattleState}", sim.MapEvent.BattleState);

                // Tell the spectators the simulation is over, otherwise they stay stuck in the spectator window now
                // that the pacing client (which would have driven it to completion) is gone.
                network.SendAll(new NetworkBattleSimulationFinished(entry.Key));
            }
        }, blocking: true);

        lock (simLock)
        {
            foreach (var entry in orphaned)
                activeSimulations.Remove(entry.Key);
        }
    }

    /// <summary>
    /// [Server, main thread] End the simulation session: commit XP and release the simulation troop
    /// allocations (mirroring <c>MapEvent.SimulateBattleRoundEndSession</c>), then restore the observer
    /// that was swapped out when the simulation began.
    /// </summary>
    private static void EndSimulationSession(ActiveSimulation sim)
    {
        foreach (var side in sim.MapEvent._sides)
        {
            sim.MapEvent.CommitXpGains();
            side.EndSimulation();
        }

        sim.MapEvent.BattleObserver = sim.PreviousObserver;
    }

    private bool TryResolveCharacterObject(string objectId, bool isHero, out CharacterObject characterObject)
    {
        characterObject = null;

        if (isHero)
        {
            if (!objectManager.TryGetObject<Hero>(objectId, out var hero))
                return false;

            characterObject = hero.CharacterObject;
            return characterObject != null;
        }

        return objectManager.TryGetObject(objectId, out characterObject);
    }
}
