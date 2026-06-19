using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Missions.Agents.Handlers;
using GameInterface.Missions.Agents.Messages;
using GameInterface.Missions.BoardGames;
using GameInterface.Missions.Missiles;
using GameInterface.Missions.Missiles.Handlers;
using GameInterface.Missions.Services.Network;
using GameInterface.Missions.Services.Network.Data;
using GameInterface.Missions.Services.Network.Messages;
using GameInterface.Services.Armies;
using GameInterface.Services.Entity;
using GameInterface.Services.Locations;
using GameInterface.Services.Locations.Messages;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Concurrent;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Missions.Services.Taverns
{
    public class CoopTavernsController : CoopMissionController, ILocationMissionBehavior
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CoopTavernsController>();

        private readonly IControllerIdProvider controllerIdProvider;
        //private readonly BoardGameManager boardGameManager;

        // The handler set is passed to the base, which owns disposing them on mission end. AgentMovementHandler
        // (first) is the one that both broadcasts movement and cleans up a peer's agents on disconnect/reconnect;
        // without it a leaver's agent is never removed and a rejoining player is deduped as "already registered".
        public CoopTavernsController(
            IBattleNetwork network,
            IMessageBroker messageBroker,
            IControllerIdProvider controllerIdProvider,
            //BoardGameManager boardGameManager,
            IObjectManager objectManager,
            ICoopMissionComponent coopMissionComponent)
            : base(network, messageBroker, objectManager, coopMissionComponent)
        {
            this.controllerIdProvider = controllerIdProvider;
            //this.boardGameManager = boardGameManager;

            messageBroker.Subscribe<NetworkLeaveMission>(Handle_LeaveMission);
            messageBroker.Subscribe<PlayerEnteredLocation>(Handle_PlayerEnteredLocation);
        }

        // Read on the network thread (HandleJoinInfo gate) and written on the main thread
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

            string controllerId = controllerIdProvider.ControllerId;

            Guid agentId = Guid.NewGuid();

            var agentRegistry = coopMissionComponent.AgentRegistry;

            if (!agentRegistry.TryRegisterAgent(controllerId, agentId, Agent.Main))
                return;

            if (!agentRegistry.TryGetAgentInfo(Agent.Main, out var agentInfo))
                return;

            _localAgentRegistered = true;
            Logger.Information("[LocationSync] Registered local agent (hero {AgentId}) for {PlayerID}; broadcasting join info",
                agentId, controllerId);

            if (!objectManager.TryGetIdWithLogging(CharacterObject.PlayerCharacter, out var characterObjectId))
                return;

            // Announce to peers that connected before this controller was attached/subscribed
            // (their PeerConnected was missed). Remote side dedupes by the party/agent id.
            network.SendAll(BuildJoinInfo(agentInfo, characterObjectId));

            // The mission is now set up (player agent + teams exist). Spawn any join info that arrived
            // before we were ready.
            DrainPendingJoinInfos();
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

            // Just start the socket — do NOT open a connection to the server. NAT introduction
            // (SendNatIntroduceRequest below) is an unconnected message, and the co-host CoopServer accepts
            // any connection as a campaign player, so connecting here would register a phantom peer. The
            // relay fallback (ConnectToP2PServer) is deferred until the server can distinguish it.
            network.Start();

            // '|' separator, NOT '%': ConnectionToken serializes as PeerId%InstanceId%NatType and splits
            // on '%', so a '%' inside the instance id would break token parsing on both ends.
            network.ConnectToInstance($"{settlementId}|{locationId}");
            coopMissionComponent.AgentRegistry.Clear();
        }

        private NetworkMissionJoinInfo BuildJoinInfo(CoopAgentInfo agentInfo, string characterObjectId)
        {
            bool isPlayerAlive = Agent.Main.Health > 0;
            Vec3 position = Agent.Main.Position;
            float health = Agent.Main.Health;

            var agents = new CoopAgentSpawnData[]
            {
                new CoopAgentSpawnData(agentInfo.AgentId, characterObjectId, position, health, isPlayer: true),
            };

            return new NetworkMissionJoinInfo(
                controllerIdProvider.ControllerId,
                isPlayerAlive,
                agents
            );
        }

        protected override void SendJoinInfo(string controllerId)
        {
            Logger.Debug("Sending join request");

            if (_localAgentRegistered == false || Agent.Main == null)
            {
                Logger.Information("[LocationSync] Skipping join info to {Controller} — local party not registered yet (will re-announce on render)", controllerId);
                return;
            }

            if (!objectManager.TryGetIdWithLogging(CharacterObject.PlayerCharacter, out var characterObjectId))
                return;

            var agentRegistry = coopMissionComponent.AgentRegistry;

            if (!agentRegistry.TryGetAgentInfo(Agent.Main, out var agentInfo))
                return;

            NetworkMissionJoinInfo request = BuildJoinInfo(agentInfo, characterObjectId);

            network.Send(controllerId, request);
            Logger.Information("Sent Join Request for {PlayerID} to {Controller}", request.ControllerId, controllerId);
        }

        protected override void OnLeaving()
        {
            network.SendAll(new NetworkLeaveMission(controllerIdProvider.ControllerId));
            messageBroker.Publish(this, new PlayerLeftLocation());
            network.Stop();
        }

        public override void Dispose()
        {
            messageBroker.Unsubscribe<NetworkLeaveMission>(Handle_LeaveMission);
            messageBroker.Unsubscribe<PlayerEnteredLocation>(Handle_PlayerEnteredLocation);

            base.Dispose();
        }

        private void Handle_LeaveMission(MessagePayload<NetworkLeaveMission> payload)
        {
            string leftControllerId = payload.What.ControllerId;

            // Our own broadcast echoed back by a peer — ignore.
            if (leftControllerId == controllerIdProvider.ControllerId) return;

            var agentRegistry = coopMissionComponent.AgentRegistry;

            int removedCount = 0;
            foreach (var agentInfo in agentRegistry.GetAgents(leftControllerId))
            {
                if (agentRegistry.TryGetAgentInfo(agentInfo.AgentId, out var info) == false) continue;

                Agent agent = info.Agent;
                GameThread.Run(() =>
                {
                    if (agent != null && agent.Health > 0)
                    {
                        agent.MakeDead(false, ActionIndexCache.act_none);
                        agent.FadeOut(false, true);
                    }
                });

                agentRegistry.RemoveAgent(agentInfo.AgentId);
                removedCount++;
            }

            Logger.Information("[LocationSync] LeaveMission from {ControllerId} — removing {AgentCount} agents", leftControllerId, removedCount);
        }

        protected override void HandleJoinInfo(NetPeer netPeer, NetworkMissionJoinInfo joinInfo)
        {
            // Spawning needs the interior mission fully set up (player agent + teams). On a rejoin the join
            // info beats the mission setup, so buffer it and drain once we are ready (TryRegisterLocalAgent).
            // Re-check readiness after enqueuing to close the race with the main thread flipping it.
            if (_localAgentRegistered == false)
            {
                _pendingJoinInfos.Enqueue((netPeer, joinInfo));
                Logger.Information("[LocationSync] Mission not ready — buffered join info for {ControllerId}", joinInfo.ControllerId);
                DrainPendingJoinInfos();
                return;
            }

            ProcessJoinInfo(netPeer, joinInfo);
        }

        private void ProcessJoinInfo(NetPeer netPeer, NetworkMissionJoinInfo joinInfo)
        {
            foreach (var agentData in joinInfo.AiAgentData)
            {
                ProcessAgent(joinInfo.ControllerId, agentData);
            }
        }

        private void ProcessAgent(string controllerId, CoopAgentSpawnData agentData)
        {
            if (agentData.AgentId == Guid.Empty)
            {
                Logger.Warning("[LocationSync] Join info from {ControllerId} has no agent id — skipping", controllerId);
                return;
            }

            var agentRegistry = coopMissionComponent.AgentRegistry;

            // Dedupe across all peers: NAT punch can yield more than one connection to the same remote
            // client, delivering its join info multiple times. Only spawn one agent per id.
            if (agentRegistry.TryGetAgentInfo(agentData.AgentId, out _))
            {
                // On a clean rejoin this should NOT fire — if it does, the leaver's agent was left in the
                // registry on leave/disconnect (stale collection), which blocks the re-spawn.
                Logger.Information("[LocationSync] Agent {AgentID} already registered — skipping spawn (expected only for duplicate NAT connections, NOT on rejoin)", agentData.AgentId);
                return;
            }

            if (!objectManager.TryGetObjectWithLogging(agentData.CharacterObjectId, out CharacterObject characterObject))
                return;

            Logger.Information("Spawning {AgentType} called {AgentName}({AgentID}) from {Peer}",
                agentData.IsPlayer == true ? "Player" : "Agent",
                characterObject?.Name?.ToString() ?? "<unresolved>", agentData.AgentId, controllerId);

            Agent newAgent = SpawnAgent(agentData.Position, characterObject);

            if (newAgent == null)
            {
                Logger.Error("[LocationSync] Failed to spawn remote agent {AgentID} — removing agent.", agentData.AgentId);
                agentRegistry.RemoveAgent(agentData.AgentId);
                return;
            }

            agentRegistry.TryRegisterAgent(controllerId, agentData.AgentId, newAgent);
            Logger.Information("[LocationSync] Spawned + registered remote agent {AgentID} at {Pos} (mission '{Scene}')",
                agentData.AgentId, newAgent.Position, Mission.Current?.SceneName);
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

            // HandleJoinInfo runs on the network thread. AgentBuildData's ctor (and SpawnAgent) touch
            // TaleWorlds engine statics (Team.Invalid -> Team.Initialize -> Formation.Reset) that must run
            // on the main thread, so build AND spawn entirely inside the game-loop closure — not just the
            // final SpawnAgent call. Doing the ctor off-thread NREs intermittently (notably on rejoin).
            Agent agent = null;
            GameThread.RunSafe(() =>
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
            }, blocking: true);

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
