using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents.Logging;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
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
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<BattleSimulationStarted>(Handle_BattleSimulationStarted);
        messageBroker.Unsubscribe<RequestAdvanceBattleSimulation>(Handle_RequestAdvanceBattleSimulation);
        messageBroker.Unsubscribe<NetworkRequestRunBattleSimulation>(Handle_NetworkRequestRunBattleSimulation);
        messageBroker.Unsubscribe<NetworkAdvanceBattleSimulation>(Handle_NetworkAdvanceBattleSimulation);
        messageBroker.Unsubscribe<NetworkBattleSimulationRound>(Handle_NetworkBattleSimulationRound);
        messageBroker.Unsubscribe<NetworkBattleSimulationFinished>(Handle_NetworkBattleSimulationFinished);
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
            int rounds = 0;
            while (rounds < maxRounds && rounds < MaxSimulationRounds && !sim.MapEvent.HasWinner)
            {
                rounds++;
                sim.MapEvent.SimulatePlayerEncounterBattle();

                var changes = sim.Observer.FlushRound();
                if (changes.Length > 0)
                    network.Send(sim.Peer, new NetworkBattleSimulationRound(mapEventId, changes));
            }

            if (sim.MapEvent.HasWinner)
            {
                // End the simulation session: commit XP and release the simulation troop allocations,
                // mirroring MapEvent.SimulateBattleRoundEndSession.
                foreach (var side in sim.MapEvent._sides)
                {
                    sim.MapEvent.CommitXpGains();
                    side.EndSimulation();
                }

                sim.MapEvent.BattleObserver = sim.PreviousObserver;
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
            network.Send(sim.Peer, new NetworkBattleSimulationFinished(mapEventId));
        }
    }

    /// <summary>[Client] Resolve a streamed round and queue it for playback.</summary>
    private void Handle_NetworkBattleSimulationRound(MessagePayload<NetworkBattleSimulationRound> payload)
    {
        var message = payload.What;
        if (message.Changes == null || message.Changes.Length == 0)
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
    }

    /// <summary>[Client] Server finished simulating: end playback once the queued rounds drain.</summary>
    private void Handle_NetworkBattleSimulationFinished(MessagePayload<NetworkBattleSimulationFinished> payload)
    {
        if (PlayerEncounter.CurrentBattleSimulation == null)
        {
            Logger.Warning("Received {Message} but no battle simulation is active", nameof(NetworkBattleSimulationFinished));
            return;
        }

        BattleSimulationReplay.RequestFinish();
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
