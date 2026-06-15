using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Instances;
using Common.PacketHandlers;
using Missions.Services.Agents.Handlers;
using Missions.Services.Agents.Messages;
using Serilog;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Network
{
    public class CoopMissionNetworkBehavior : MissionBehavior, IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CoopMissionNetworkBehavior>();

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        /// <summary>
        /// Set by <see cref="LiveInstanceLauncher"/> in live campaign play. Gates the test-harness-only
        /// teardown (EndGame + disposing shared singletons) that must not run when leaving an interior.
        /// </summary>
        public bool IsLiveInstance { get; set; }

        private readonly LiteNetP2PClient client;

        private readonly IMessageBroker messageBroker;
        private readonly INetworkAgentRegistry agentRegistry;

        private readonly IDisposable[] disposables;

        public CoopMissionNetworkBehavior(
            LiteNetP2PClient client,
            IMessageBroker messageBroker,
            INetworkAgentRegistry agentRegistry,
            IAgentMovementHandler movementHandler,
            IMessagePacketHandler messagePacketHandler)
        {
            this.client = client;
            this.messageBroker = messageBroker;
            this.agentRegistry = agentRegistry;

            disposables = new IDisposable[]
            {
                client,
                movementHandler,
                messagePacketHandler
            };
        }

        ~CoopMissionNetworkBehavior() => Dispose();

        public void Dispose()
        {
            agentRegistry.Clear();

            // Live: the client/handlers are singletons reused across locations (launcher owns them).
            // Disposing on mission end would leave the next location wired to dead, unsubscribed handlers.
            if (IsLiveInstance) return;

            foreach (var disposable in disposables)
            {
                disposable.Dispose();
            }
        }

        public override void OnEndMission()
        {
            base.OnEndMission();

            // Only the test harness (mission == whole session) drops to the main menu on end.
            if (IsLiveInstance == false)
            {
                MBGameManager.EndGame();
            }

            Dispose();
        }

        public override void OnRemoveBehavior()
        {
            base.OnRemoveBehavior();
            Dispose();
        }

        public override void OnRenderingStarted()
        {
            // In a live campaign the server assigns a unique instance id per settlement interior;
            // fall back to the scene name for the standalone mission test harness (no campaign server).
            string instance = InstanceContext.Instance.InInstance
                ? InstanceContext.Instance.CurrentInstanceId
                : Mission.SceneName;

            client.NatPunch(instance);
        }

        public override void OnAgentDeleted(Agent affectedAgent)
        {
            messageBroker.Publish(this, new AgentDeleted(affectedAgent));

            base.OnAgentDeleted(affectedAgent);
        }
    }
}
