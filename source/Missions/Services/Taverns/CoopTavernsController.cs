using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.Locations;
using GameInterface.Services.Locations.Messages;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
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

        private readonly LiteNetP2PClient network;
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
        private readonly IDisposable[] handlers;

        // The campaign network config — the same INetworkConfiguration CoopClient is connected to. Its
        // Address/Port are the rendezvous server; the P2P client's own config is pointed at it before
        // connecting so the NAT punch / relay reaches the co-host instead of compiled-in defaults.
        private readonly INetworkConfiguration campaignConfiguration;

        public CoopTavernsController(
            LiteNetP2PClient network,
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

            handlers = new IDisposable[]
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

        private bool _localAgentRegistered;
        private bool _instanceRequested;

        public override void OnCreated()
        {
            TryRegisterLocalAgent();
        }

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
            bool isPlayerAlive = Agent.Main != null && Agent.Main.Health > 0;
            Vec3 position = Agent.Main?.Position ?? default;
            float health = Agent.Main?.Health ?? 0;

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

            if (!objectManager.TryGetIdWithLogging(CharacterObject.PlayerCharacter, out var characterObjectId))
                return;

            NetworkMissionJoinInfo request = BuildJoinInfo(characterObjectId);

            network.Send(peer, request);
            Logger.Information("Sent Join Request for {PlayerID} to {Peer}", request.ControllerId, peer);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkMissionJoinInfo>(Handle_JoinInfo);
            messageBroker.Unsubscribe<NetworkLeaveMission>(Handle_LeaveMission);
            messageBroker.Unsubscribe<PeerConnected>(Handle_PeerConnected);
            messageBroker.Unsubscribe<PlayerEnteredLocation>(Handle_PlayerEnteredLocation);
        }

        public override void OnEndMission()
        {
            // Deliberate leave: tell mesh peers to drop our agent NOW, before we drop the P2P connection.
            network.SendAll(new NetworkLeaveMission(controllerIdProvider.ControllerId));

            // Drop the peers but keep the socket/poller alive so the next location reuses it without a
            // fragile Stop/Start (which churns the port and re-enters the Poller). DisconnectPeers flushes
            // the queued LeaveMission before dropping the connections.
            network.DisconnectPeers();

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
            Vec3 startingPos = joinInfo.StartingPosition;

            Logger.Information("[LocationSync] Received join info for {AgentID} from {Peer} at pos {Pos}", newAgentId, netPeer, startingPos);

            // Don't spawn our own broadcast echoed back by a peer.
            if (newAgentId == controllerIdProvider.ControllerId)
            {
                Logger.Debug("[LocationSync] Join info {AgentID} is our own id — ignoring", newAgentId);
                return;
            }

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

            try
            {
                AgentBuildData agentBuildData = new AgentBuildData(character);
                agentBuildData.BodyProperties(character.GetBodyPropertiesMax());
                agentBuildData.InitialPosition(startingPos);
                agentBuildData.Team(Mission.Current.PlayerAllyTeam);
                agentBuildData.InitialDirection(Vec2.Forward);
                agentBuildData.NoHorses(true);
                agentBuildData.Equipment(character.FirstCivilianEquipment);
                agentBuildData.TroopOrigin(new SimpleAgentOrigin(character, -1, null, default));
                agentBuildData.Controller(AgentControllerType.None);

                Agent agent = null;
                GameLoopRunner.RunOnMainThread(() =>
                {
                    agent = Mission.Current.SpawnAgent(agentBuildData);
                    agent.FadeIn();
                }, true);

                return agent;
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "[LocationSync] Build/spawn failed for character '{Name}'", character?.StringId ?? "<null>");
                return null;
            }
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
