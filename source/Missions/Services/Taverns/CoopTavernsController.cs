using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Instances.Messages;
using LiteNetLib;
using Missions.Services.BoardGames;
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
    public class CoopTavernsController : MissionBehavior, IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CoopArenaController>();
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        private readonly INetwork network;
        private readonly IMessageBroker _messageBroker;
        private readonly INetworkAgentRegistry _agentRegistry;

        private readonly BoardGameManager _boardGameManager;

        // Stable identity for the local player, NOT regenerated per controller. The remote side dedupes
        // spawns by this id (Handle_JoinInfo), so every CoopTavernsController the local player produces
        // for the same instance MUST broadcast the same id. The double OpenIndoorMission / a duplicate
        // NAT connection can otherwise yield a second controller with a fresh Guid.NewGuid(), which the
        // remote spawns as a phantom, never-moving duplicate. A process-wide id collapses all of these
        // to one agent. (Re-entry is safe: the peer disconnect on leave despawns the remote agent first.)
        private static readonly Guid LocalPlayerId = Guid.NewGuid();

        private readonly Guid playerId;

        public CoopTavernsController(
            INetwork network,
            IMessageBroker messageBroker,
            INetworkAgentRegistry agentRegistry,
            BoardGameManager boardGameManager)
        {
            this.network = network;
            _messageBroker = messageBroker;
            _agentRegistry = agentRegistry;
            _boardGameManager = boardGameManager;

            playerId = LocalPlayerId;

            messageBroker.Subscribe<NetworkMissionJoinInfo>(Handle_JoinInfo);
            messageBroker.Subscribe<PeerConnected>(Handle_PeerConnected);
        }

        private bool _localAgentRegistered;

        public override void AfterStart()
        {
            TryRegisterLocalAgent();
        }

        // When this controller is attached AFTER the mission has already started (the live
        // location-sync bridge path), AfterStart never fires, so the local agent is never registered
        // as a controlled agent and AgentMovementHandler never broadcasts its movement (remote players
        // see it frozen). Retry on tick until Agent.Main exists and is registered.
        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (_localAgentRegistered == false) TryRegisterLocalAgent();
        }

        private void TryRegisterLocalAgent()
        {
            if (_localAgentRegistered) return;
            if (Agent.Main == null) return;

            if (_agentRegistry.RegisterControlledAgent(playerId, Agent.Main))
            {
                _localAgentRegistered = true;
                Logger.Information("[LocationSync] Registered local controlled agent {PlayerID}; broadcasting join info", playerId);

                // Cover peers that connected before this controller was attached/subscribed
                // (their PeerConnected was missed). Remote side dedupes by player id.
                network.SendAll(BuildJoinInfo());
            }
        }

        private void Handle_PeerConnected(MessagePayload<PeerConnected> obj)
        {
            Logger.Information("[LocationSync] P2P peer connected {Peer}; sending join info {PlayerID}", obj.What.Peer, playerId);
            SendJoinInfo(obj.What.Peer);
        }

        private NetworkMissionJoinInfo BuildJoinInfo()
        {
            CharacterObject characterObject = CharacterObject.PlayerCharacter;
            bool isPlayerAlive = Agent.Main != null && Agent.Main.Health > 0;
            Vec3 position = Agent.Main?.Position ?? default;
            float health = Agent.Main?.Health ?? 0;

            return new NetworkMissionJoinInfo(
                characterObject,
                isPlayerAlive,
                playerId,
                position,
                health,
                null);
        }

        private void SendJoinInfo(NetPeer peer)
        {
            Logger.Debug("Sending join request");

            NetworkMissionJoinInfo request = BuildJoinInfo();

            network.Send(peer, request);
            Logger.Information("Sent Join Request for {PlayerID} to {Peer}", request.PlayerId, peer);
        }

        public void Dispose()
        {
            _messageBroker.Unsubscribe<NetworkMissionJoinInfo>(Handle_JoinInfo);
            _messageBroker.Unsubscribe<PeerConnected>(Handle_PeerConnected);
        }

        public override void OnEndMission()
        {
            // Left the instance (tavern). Signal the campaign side to reset InstanceContext and tear
            // down the P2P client. The server releases membership separately via NetworkPlayerCampaignEntered.
            _messageBroker.Publish(this, new InstanceCleared());

            base.OnEndMission();
            Dispose();
        }

        private void Handle_JoinInfo(MessagePayload<NetworkMissionJoinInfo> payload)
        {
            NetPeer netPeer = payload.Who as NetPeer ?? throw new InvalidCastException("Payload 'Who' was not of type NetPeer");

            NetworkMissionJoinInfo joinInfo = payload.What;

            Guid newAgentId = joinInfo.PlayerId;
            Vec3 startingPos = joinInfo.StartingPosition;

            Logger.Information("[LocationSync] Received join info for {AgentID} from {Peer} at pos {Pos}", newAgentId, netPeer, startingPos);

            // Don't spawn our own broadcast echoed back by a peer.
            if (newAgentId == playerId)
            {
                Logger.Debug("[LocationSync] Join info {AgentID} is our own id — ignoring", newAgentId);
                return;
            }

            // Dedupe across all peers: NAT punch can yield more than one connection to the same remote
            // client, delivering its join info multiple times. Only spawn one agent per player id.
            if (_agentRegistry.IsAgentRegistered(newAgentId))
            {
                Logger.Debug("[LocationSync] Agent {AgentID} already registered — skipping duplicate spawn", newAgentId);
                return;
            }

            Logger.Information("Spawning {EntityType} called {AgentName}({AgentID}) from {Peer}",
                joinInfo.CharacterObject?.IsPlayerCharacter == true ? "Player" : "Agent",
                joinInfo.CharacterObject?.Name?.ToString() ?? "<unresolved>", newAgentId, netPeer);


            Agent newAgent = SpawnAgent(startingPos, joinInfo.CharacterObject);
            if (newAgent == null)
            {
                Logger.Error("[LocationSync] Failed to spawn remote agent {AgentID} — skipping registration.", newAgentId);
                return;
            }
            _agentRegistry.RegisterNetworkControlledAgent(netPeer, newAgentId, newAgent);
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
                character = CharacterObject.PlayerCharacter;
            }

            var agent = TryBuildAndSpawn(character, startingPos);
            if (agent != null) return agent;

            // The cheap pre-check can miss other null internals; retry once with the local player char.
            var local = CharacterObject.PlayerCharacter;
            if (ReferenceEquals(local, character) == false)
            {
                Logger.Warning("[LocationSync] Spawn with the supplied character failed; retrying with the local player character. REPORT THIS.");
                agent = TryBuildAndSpawn(local, startingPos);
            }

            if (agent == null)
                Logger.Error("[LocationSync] Could not spawn remote agent with either the remote or local character.");

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

        private Agent TryBuildAndSpawn(CharacterObject character, Vec3 startingPos)
        {
            if (character == null) return null;

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

                Agent agent = default;
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
    }
}
