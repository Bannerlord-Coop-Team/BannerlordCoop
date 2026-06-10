using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using GameInterface.Services.MapEvents.Logging;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Serilog;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Handlers;

/// <summary>
/// Runs auto-resolve battle simulations authoritatively on the server, paced by the requesting client.
/// </summary>
/// <remarks>
/// Client: opening the screen (<see cref="BattleSimulationStarted"/>) sends <see cref="NetworkRequestRunBattleSimulation"/>;
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

        messageBroker.Subscribe<BattleSimulationStarted>(Handle_BattleSimulationStarted);
        messageBroker.Subscribe<RequestAdvanceBattleSimulation>(Handle_RequestAdvanceBattleSimulation);
        messageBroker.Subscribe<NetworkRequestRunBattleSimulation>(Handle_NetworkRequestRunBattleSimulation);
        messageBroker.Subscribe<NetworkAdvanceBattleSimulation>(Handle_NetworkAdvanceBattleSimulation);
        messageBroker.Subscribe<NetworkBattleSimulationRound>(Handle_NetworkBattleSimulationRound);
        messageBroker.Subscribe<NetworkBattleSimulationFinished>(Handle_NetworkBattleSimulationFinished);
        messageBroker.Subscribe<NetworkOpenBattleSimulation>(Handle_NetworkOpenBattleSimulation);
        messageBroker.Subscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<BattleSimulationStarted>(Handle_BattleSimulationStarted);
        messageBroker.Unsubscribe<RequestAdvanceBattleSimulation>(Handle_RequestAdvanceBattleSimulation);
        messageBroker.Unsubscribe<NetworkRequestRunBattleSimulation>(Handle_NetworkRequestRunBattleSimulation);
        messageBroker.Unsubscribe<NetworkAdvanceBattleSimulation>(Handle_NetworkAdvanceBattleSimulation);
        messageBroker.Unsubscribe<NetworkBattleSimulationRound>(Handle_NetworkBattleSimulationRound);
        messageBroker.Unsubscribe<NetworkBattleSimulationFinished>(Handle_NetworkBattleSimulationFinished);
        messageBroker.Unsubscribe<NetworkOpenBattleSimulation>(Handle_NetworkOpenBattleSimulation);
        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_PlayerDisconnected);
    }

    /// <summary>[Client] Begin local playback and ask the server to set the simulation up.</summary>
    private void Handle_BattleSimulationStarted(MessagePayload<BattleSimulationStarted> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.MapEvent, out var mapEventId))
            return;

        BattleSimulationReplay.Begin(mapEventId);

        mapEventLogger.DebugMapEvent(payload.What.MapEvent, "Requesting server-side battle simulation");

        // On a client, SendAll targets the server (its only connected peer).
        network.SendAll(new NetworkRequestRunBattleSimulation(mapEventId));
    }

    /// <summary>[Client] Forward a playback-paced advance to the server.</summary>
    private void Handle_RequestAdvanceBattleSimulation(MessagePayload<RequestAdvanceBattleSimulation> payload)
    {
        network.SendAll(new NetworkAdvanceBattleSimulation(payload.What.MapEventId, payload.What.Rounds));
    }

    /// <summary>[Server] Set the simulation up (no rounds yet); the client paces it via advances.</summary>
    private void Handle_NetworkRequestRunBattleSimulation(MessagePayload<NetworkRequestRunBattleSimulation> payload)
    {
        if (!ModInformation.IsServer)
            return;

        if (!(payload.Who is NetPeer requestingPeer))
        {
            Logger.Error("Received {Message} with no originating peer", nameof(NetworkRequestRunBattleSimulation));
            return;
        }

        var mapEventId = payload.What.MapEventId;

        if (!objectManager.TryGetObjectWithLogging<MapEvent>(mapEventId, out var mapEvent))
            return;

        if (mapEvent.HasWinner)
        {
            mapEventLogger.DebugMapEvent(mapEvent, "Battle simulation requested for an already finished map event; replying immediately");
            network.Send(requestingPeer, new NetworkBattleSimulationFinished(mapEventId));
            return;
        }

        var observer = new ForwardingBattleObserver(objectManager);

        GameLoopRunner.RunOnMainThread(() =>
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
        network.SendAll(new NetworkOpenBattleSimulation(mapEventId));

        mapEventLogger.DebugMapEvent(mapEvent, "Battle simulation set up; awaiting client-paced advances");
    }

    /// <summary>[Server] Resolve the requested number of rounds, streaming each round back.</summary>
    private void Handle_NetworkAdvanceBattleSimulation(MessagePayload<NetworkAdvanceBattleSimulation> payload)
    {
        if (!ModInformation.IsServer)
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

        GameLoopRunner.RunOnMainThread(() =>
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
        GameLoopRunner.RunOnMainThread(() =>
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

    /// <summary>[Client] Server finished simulating: end playback once the queued rounds drain.</summary>
    private void Handle_NetworkBattleSimulationFinished(MessagePayload<NetworkBattleSimulationFinished> payload)
    {
        // Both the encounter state and the replay's finish flag belong to the main-thread tick.
        GameLoopRunner.RunOnMainThread(() =>
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
        if (!ModInformation.IsClient)
            return;

        var mapEventId = payload.What.MapEventId;

        GameLoopRunner.RunOnMainThread(() =>
        {
            // Initiator already has it open and is pacing it.
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

            // Spectator mode must be set before StartBattleSimulation: its postfix checks it to avoid requesting a
            // second authoritative run. InitSimulation(null, null) builds the scoreboard from the event's parties
            // (full battle); the streamed rounds then drive it.
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
        if (!ModInformation.IsServer)
            return;

        var peer = payload.What.PlayerId;

        List<KeyValuePair<string, ActiveSimulation>> orphaned;
        lock (simLock)
        {
            orphaned = activeSimulations.Where(entry => entry.Value.Peer == peer).ToList();
        }

        if (orphaned.Count == 0)
            return;

        GameLoopRunner.RunOnMainThread(() =>
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
