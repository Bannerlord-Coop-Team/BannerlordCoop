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
        /// True when this behavior runs inside a live campaign (the location-sync tavern bridge) rather
        /// than the standalone Missions test harness. In the test harness the mission IS the whole game
        /// session, so ending it ends the game; in a live campaign the mission is just an interior, so
        /// ending it must return to the settlement — never tear the campaign down to the main menu.
        /// Set by <see cref="LiveInstanceLauncher"/> when it attaches the behavior.
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

            foreach (var disposable in disposables)
            {
                disposable.Dispose();
            }
        }

        public override void OnEndMission()
        {
            base.OnEndMission();

            // Only the standalone test harness should drop to the main menu on mission end. In a live
            // campaign, leaving the tavern must keep the campaign running (the game returns the player
            // to the settlement on its own).
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
