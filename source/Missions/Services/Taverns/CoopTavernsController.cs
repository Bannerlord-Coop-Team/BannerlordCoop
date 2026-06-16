using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.Locations;
using GameInterface.Services.Locations.Messages;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using System.Collections.Concurrent;
using Missions.Services.Agents.Handlers;
using Missions.Services.BoardGames;
using Missions.Services.Missiles;
using Missions.Services.Missiles.Handlers;
using Missions.Services.Network;
using Missions.Services.Network.Messages;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Taverns
{
    public class CoopTavernsController : MissionBehavior, ILocationMissionBehavior, IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CoopArenaController>();
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        private readonly IMeshNetwork network;
        private readonly IMessageBroker messageBroker;
        private readonly INetworkAgentRegistry agentRegistry;
        private readonly IControllerIdProvider controllerIdProvider;
        private readonly BoardGameManager boardGameManager;
        private readonly IObjectManager objectManager;

        // The sync handlers. Held only to force their construction — they are InstancePerLifetimeScope
        // singletons in the shared client container, so the container owns their lifetime/disposal; this
        // controller must NOT dispose them or a re-entered tavern would get already-disposed handlers.
        // Critically, AgentMovementHandler is the one that both broadcasts movement and cleans up a peer's
        // agents on disconnect/reconnect; without it a leaver's agent is never removed and a rejoining
        // player is deduped as "already registered".
        private readonly IDisposable[] disposables;

        // The campaign network config — the same INetworkConfiguration CoopClient is connected to. Its
        // Address/Port are the rendezvous server; the P2P client's own config is pointed at it before
        // connecting so the NAT punch / relay reaches the co-host instead of compiled-in defaults.
        private readonly INetworkConfiguration campaignConfiguration;

        public CoopTavernsController(
            IMeshNetwork network,
            IMessageBroker messageBroker,
            INetworkAgentRegistry agentRegistry,
            IControllerIdProvider controllerIdProvider,
            BoardGameManager boardGameManager,
            IObjectManager objectManager,
            INetworkConfiguration campaignConfiguration,
            IAgentMovementHandler agentMovementHandler,
            IMissileHandler missileHandler,
            IWeaponDropHandler weaponDropHandler,
            IWeaponPickupHandler weaponPickupHandler,
            IShieldDamageHandler shieldDamageHandler,
            IAgentDamageHandler agentDamageHandler,
            IAgentDeathHandler agentDeathHandler,
            INetworkMissileRegistry networkMissileRegistry)
        {
            this.network = network;
            this.messageBroker = messageBroker;
            this.agentRegistry = agentRegistry;
            this.controllerIdProvider = controllerIdProvider;
            this.boardGameManager = boardGameManager;
            this.objectManager = objectManager;
            this.campaignConfiguration = campaignConfiguration;

            disposables = new IDisposable[]
            {
                agentMovementHandler,
                missileHandler,
                weaponDropHandler,
                weaponPickupHandler,
                shieldDamageHandler,
                agentDamageHandler,
                agentDeathHandler,
                networkMissileRegistry,
            };

            messageBroker.Subscribe<NetworkMissionJoinInfo>(Handle_JoinInfo);
            messageBroker.Subscribe<NetworkLeaveMission>(Handle_LeaveMission);
            messageBroker.Subscribe<PeerConnected>(Handle_PeerConnected);
            messageBroker.Subscribe<PlayerEnteredLocation>(Handle_PlayerEnteredLocation);
        }

        // Read on the network thread (Handle_JoinInfo gate) and written on the main thread
        // (TryRegisterLocalAgent), so volatile to ensure the gate sees the flip promptly.
        private volatile bool _localAgentRegistered;
        private bool _instanceRequested;

        // Join info can arrive before the local interior mission has finished setting up (teams/player
        // agent) — notably on a rejoin, where the kept-alive socket reconnects and delivers the peer's
        // join info almost immediately. Spawning a remote agent into a not-yet-initialized mission corrupts
        // team setup, so buffer early join info and drain it once we are ready (see TryRegisterLocalAgent).
        private readonly ConcurrentQueue<(NetPeer peer, NetworkMissionJoinInfo info)> _pendingJoinInfos
            = new ConcurrentQueue<(NetPeer, NetworkMissionJoinInfo)>();

        public override void OnRenderingStarted()
        {
            TryRegisterLocalAgent();
        }

        private void TryRegisterLocalAgent()
        {
            if (_localAgentRegistered) return;
            if (Agent.Main == null) return;

            if (agentRegistry.RegisterControlledAgent(controllerIdProvider.ControllerId, Agent.Main))
            {
                _localAgentRegistered = true;
                Logger.Information("[LocationSync] Registered local controlled agent {PlayerID}; broadcasting join info", controllerIdProvider.ControllerId);

                if (!objectManager.TryGetIdWithLogging(CharacterObject.PlayerCharacter, out var characterObjectId))
                    return;

                // Announce to peers that connected before this controller was attached/subscribed
                // (their PeerConnected was missed). Remote side dedupes by the stable player id.
                network.SendAll(BuildJoinInfo(characterObjectId));

                // The mission is now set up (player agent + teams exist). Spawn any join info that arrived
                // before we were ready.
                DrainPendingJoinInfos();
            }
        }

        private void DrainPendingJoinInfos()
        {
            if (_localAgentRegistered == false) return;

            while (_pendingJoinInfos.TryDequeue(out var pending))
            {
                ProcessJoinInfo(pending.peer, pending.info);
            }
        }

        // The interior mission was opened locally. This controller is attached to the mission by the
        // OpenIndoorMission postfix BEFORE the event is published, so it is the live, mission-scoped owner
        // of the P2P connection. The instance id is derived locally from (settlement, location): the
        // server creates the instance on the first NAT punch, so no separate assignment round-trip is
        // needed and both co-located clients independently compute the same id.
        private void Handle_PlayerEnteredLocation(MessagePayload<PlayerEnteredLocation> payload)
        {
            // OpenIndoorMission fires several times per entry; connect once per mission.
            if (_instanceRequested) return;

            var data = payload.What;

            if (data.Settlement == null)
            {
                Logger.Warning("[LocationSync] PlayerEnteredLocation with no settlement — skipping instance request");
                return;
            }

            if (objectManager.TryGetIdWithLogging(data.Settlement, out var settlementId) == false)
            {
                Logger.Warning("[LocationSync] Could not resolve settlement id for '{Settlement}' — skipping instance request", data.Settlement.StringId);
                return;
            }

            if (objectManager.TryGetIdWithLogging(data.Location, out var locationId) == false)
            {
                Logger.Warning("[LocationSync] Could not resolve location id for '{Location}' — skipping instance request", data.Location.StringId);
                return;
            }

            _instanceRequested = true;
            Logger.Information("[LocationSync] Requesting P2P instance settlement={SettlementId} location={LocationId}", settlementId, locationId);

            // Point the P2P client's rendezvous at the same server CoopClient is connected to, so the NAT
            // punch reaches the co-host instead of the compiled-in defaults.
            network.SetRendezvous(campaignConfiguration.Address, campaignConfiguration.Port);

            // Just start the socket — do NOT open a connection to the server. NAT introduction
            // (SendNatIntroduceRequest below) is an unconnected message, and the co-host CoopServer accepts
            // any connection as a campaign player, so connecting here would register a phantom peer. The
            // relay fallback (ConnectToP2PServer) is deferred until the server can distinguish it.
            network.Start();

            // '|' separator, NOT '%': ConnectionToken serializes as PeerId%InstanceId%NatType and splits
            // on '%', so a '%' inside the instance id would break token parsing on both ends.
            network.ConnectToInstance($"{settlementId}|{locationId}");
            agentRegistry.Clear();
        }

        private void Handle_PeerConnected(MessagePayload<PeerConnected> obj)
        {
            Logger.Information("[LocationSync] P2P peer connected {Peer}; sending join info {PlayerID}", obj.What.Peer, controllerIdProvider.ControllerId);
            SendJoinInfo(obj.What.Peer);
        }

        private NetworkMissionJoinInfo BuildJoinInfo(string characterObjectId)
        {
            bool isPlayerAlive = Agent.Main.Health > 0;
            Vec3 position = Agent.Main.Position;
            float health = Agent.Main.Health;

            return new NetworkMissionJoinInfo(
                characterObjectId,
                isPlayerAlive,
                controllerIdProvider.ControllerId,
                position,
                health,
                null);
        }

        private void SendJoinInfo(NetPeer peer)
        {
            Logger.Debug("Sending join request");

            if (Agent.Main == null)
            {
                Logger.Information("[LocationSync] Skipping join info to {Peer} — local Agent.Main not ready yet (will re-announce on render)", peer);
                return;
            }

            if (!objectManager.TryGetIdWithLogging(CharacterObject.PlayerCharacter, out var characterObjectId))
                return;

            NetworkMissionJoinInfo request = BuildJoinInfo(characterObjectId);

            network.Send(peer, request);
            Logger.Information("Sent Join Request for {PlayerID} to {Peer}", request.ControllerId, peer);
        }

        public void Dispose()
        {
            foreach (var disposable in disposables)
            {
                disposable.Dispose();
            }

            messageBroker.Unsubscribe<NetworkMissionJoinInfo>(Handle_JoinInfo);
            messageBroker.Unsubscribe<NetworkLeaveMission>(Handle_LeaveMission);
            messageBroker.Unsubscribe<PeerConnected>(Handle_PeerConnected);
            messageBroker.Unsubscribe<PlayerEnteredLocation>(Handle_PlayerEnteredLocation);
        }

        public override void OnEndMission()
        {
            // Deliberate leave: tell mesh peers to drop our agent NOW, before we drop the P2P connection.
            network.SendAll(new NetworkLeaveMission(controllerIdProvider.ControllerId));

            // Full teardown on exit: Stop() drops the peers (flushing the queued LeaveMission first via
            // DisconnectPeers), then stops the poller and the socket. The next location entry calls Start()
            // again on this singleton client, rebinding a fresh socket/poller — so a re-entry reconnects
            // from scratch rather than via a kept-alive mapping (which avoids the instant-reconnect races).
            network.Stop();

            // Clear our stored peers/agents now, while we're leaving and nothing is racing it. Doing this
            // here (rather than only on the next entry) means re-entry starts from a clean registry without
            // the network thread concurrently delivering join info during the clear.
            agentRegistry.Clear();

            base.OnEndMission();
            Dispose();
        }

        private void Handle_LeaveMission(MessagePayload<NetworkLeaveMission> payload)
        {
            string leftAgentId = payload.What.ControllerId;

            // Our own broadcast echoed back by a peer — ignore.
            if (leftAgentId == controllerIdProvider.ControllerId) return;

            Logger.Information("[LocationSync] Received LeaveMission for {AgentID} — removing remote agent", leftAgentId);

            if (agentRegistry.TryGetAgent(leftAgentId, out Agent agent))
            {
                GameLoopRunner.RunOnMainThread(() =>
                {
                    if (agent.Health > 0)
                    {
                        agent.MakeDead(false, ActionIndexCache.act_none);
                        agent.FadeOut(false, true);
                    }
                });
            }

            agentRegistry.RemoveNetworkControlledAgent(leftAgentId);
        }

        private void Handle_JoinInfo(MessagePayload<NetworkMissionJoinInfo> payload)
        {
            NetPeer netPeer = payload.Who as NetPeer ?? throw new InvalidCastException("Payload 'Who' was not of type NetPeer");

            NetworkMissionJoinInfo joinInfo = payload.What;

            string newAgentId = joinInfo.ControllerId;

            Logger.Information("[LocationSync] Received join info for {AgentID} from {Peer} at pos {Pos}", newAgentId, netPeer, joinInfo.StartingPosition);

            // Don't spawn our own broadcast echoed back by a peer.
            if (newAgentId == controllerIdProvider.ControllerId)
            {
                Logger.Debug("[LocationSync] Join info {AgentID} is our own id — ignoring", newAgentId);
                return;
            }

            // Spawning needs the interior mission fully set up (player agent + teams). On a rejoin the join
            // info beats the mission setup, so buffer it and drain once we are ready (TryRegisterLocalAgent).
            // Re-check readiness after enqueuing to close the race with the main thread flipping it.
            if (_localAgentRegistered == false)
            {
                _pendingJoinInfos.Enqueue((netPeer, joinInfo));
                Logger.Information("[LocationSync] Mission not ready — buffered join info for {AgentID}", newAgentId);
                DrainPendingJoinInfos();
                return;
            }

            ProcessJoinInfo(netPeer, joinInfo);
        }

        private void ProcessJoinInfo(NetPeer netPeer, NetworkMissionJoinInfo joinInfo)
        {
            string newAgentId = joinInfo.ControllerId;
            Vec3 startingPos = joinInfo.StartingPosition;

            // Dedupe across all peers: NAT punch can yield more than one connection to the same remote
            // client, delivering its join info multiple times. Only spawn one agent per player id.
            if (agentRegistry.IsAgentRegistered(newAgentId))
            {
                // On a clean rejoin this should NOT fire — if it does, the leaver's agent was left in the
                // registry on disconnect (stale collection), which blocks the re-spawn.
                Logger.Information("[LocationSync] Agent {AgentID} already registered — skipping spawn (expected only for duplicate NAT connections, NOT on rejoin)", newAgentId);
                return;
            }

            if (!objectManager.TryGetObjectWithLogging(joinInfo.CharacterObjectId, out CharacterObject characterObject))
                return;

            Logger.Information("Spawning {EntityType} called {AgentName}({AgentID}) from {Peer}",
                characterObject?.IsPlayerCharacter == true ? "Player" : "Agent",
                characterObject?.Name?.ToString() ?? "<unresolved>", newAgentId, netPeer);


            Agent newAgent = SpawnAgent(startingPos, characterObject);

            if (newAgent == null)
            {
                Logger.Error("[LocationSync] Failed to spawn remote agent {AgentID} — skipping registration.", newAgentId);
                return;
            }
            agentRegistry.RegisterNetworkControlledAgent(netPeer, newAgentId, newAgent);
            Logger.Information("[LocationSync] Spawned + registered remote agent {AgentID} at {Pos} (mission '{Scene}')",
                newAgentId, newAgent.Position, Mission.Current?.SceneName);
        }

        public Agent SpawnAgent(Vec3 startingPos, CharacterObject character)
        {
            // A remote player's hero CharacterObject often does not resolve to a fully-initialized
            // object on this client (live campaign: each player has a distinct, not-yet-synced hero),
            // so GetBodyPropertiesMax / FirstCivilianEquipment NRE internally. Try the supplied
            // character, then fall back to the local player character so an agent still spawns.
            // (Proper fix: sync the remote player's hero identity — see doc/LocationSync.md §7.)
            if (LooksUsable(character) == false)
            {
                Logger.Warning("[LocationSync] Remote CharacterObject '{Name}' looks unresolved (null culture/etc). " +
                    "Falling back to the local player character so an agent still spawns. REPORT THIS.",
                    character?.StringId ?? "<null>");
                return null;
            }

            // Handle_JoinInfo runs on the network thread. AgentBuildData's ctor (and SpawnAgent) touch
            // TaleWorlds engine statics (Team.Invalid -> Team.Initialize -> Formation.Reset) that must run
            // on the main thread, so build AND spawn entirely inside the game-loop closure — not just the
            // final SpawnAgent call. Doing the ctor off-thread NREs intermittently (notably on rejoin).
            Agent agent = null;
            GameLoopRunner.RunOnMainThread(() =>
            {
                try
                {
                    // The player may have left between receiving the join info and this running.
                    if (Mission.Current == null) return;

                    AgentBuildData agentBuildData = new AgentBuildData(character);
                    agentBuildData.BodyProperties(character.GetBodyPropertiesMax());
                    agentBuildData.InitialPosition(startingPos);
                    agentBuildData.Team(Mission.Current.PlayerAllyTeam);
                    agentBuildData.InitialDirection(Vec2.Forward);
                    agentBuildData.NoHorses(true);
                    agentBuildData.Equipment(character.FirstCivilianEquipment);
                    agentBuildData.TroopOrigin(new SimpleAgentOrigin(character, -1, null, default));
                    agentBuildData.Controller(AgentControllerType.None);

                    agent = Mission.Current.SpawnAgent(agentBuildData);
                    agent.FadeIn();
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "[LocationSync] Build/spawn failed for character '{Name}'", character?.StringId ?? "<null>");
                    agent = null;
                }
            }, true);

            return agent;
        }

        // Cheap, non-throwing pre-filter for the common "unresolved remote hero" case, so the normal
        // path does not rely on a thrown exception (which trips first-chance break in the debugger).
        private static bool LooksUsable(CharacterObject character)
        {
            if (character == null) return false;
            try
            {
                return character.Culture != null && character.Race >= 0;
            }
            catch
            {
                return false;
            }
        }

    }
}
