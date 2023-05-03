using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Serialization;
using LiteNetLib;
using Missions.Services.Agents.Messages;
using Missions.Services.Agents.Packets;
using Missions.Services.Agents.Patches;
using Missions.Services.Missiles;
using Missions.Services.Missiles.Handlers;
using Missions.Services.Network;
using Serilog;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Handlers
{
    /// <summary>
    /// Handler for agent damage in a battle
    /// </summary>
    public interface IAgentDamageHandler : IHandler, IDisposable
    {

    }
    /// <inheritdoc/>
    public class AgentDamageHandler : IAgentDamageHandler
    {
        private readonly static ILogger Logger = LogManager.GetLogger<AgentDamageHandler>();

        private readonly INetworkAgentRegistry networkAgentRegistry;
        private readonly INetworkMessageBroker networkMessageBroker;
        private readonly INetworkMissileRegistry networkMissileRegistry;

        public AgentDamageHandler(
            INetworkAgentRegistry networkAgentRegistry,
            INetworkMessageBroker networkMessageBroker,
            INetworkMissileRegistry networkMissileRegistry) 
        {
            this.networkAgentRegistry = networkAgentRegistry;
            this.networkMessageBroker = networkMessageBroker;
            this.networkMissileRegistry = networkMissileRegistry;

            networkMessageBroker.Subscribe<AgentDamaged>(AgentDamageCheckSend);
            networkMessageBroker.Subscribe<NetworkDamageAgent>(AgentDamageCheck);
            networkMessageBroker.Subscribe<NetworkAgentDamaged>(AgentDamageRecieve);
        }
        ~AgentDamageHandler()
        {
            Dispose();
        }
        public void Dispose()
        {
            networkMessageBroker.Unsubscribe<AgentDamaged>(AgentDamageCheckSend);
            networkMessageBroker.Unsubscribe<NetworkDamageAgent>(AgentDamageCheck);
            networkMessageBroker.Unsubscribe<NetworkAgentDamaged>(AgentDamageRecieve);
        }

        private void AgentDamageCheckSend(MessagePayload<AgentDamaged> payload)
        {

            // first, check if the attacker exists in the agent to ID groud, if not, no networking is needed (not a network agent)
            if (networkAgentRegistry.TryGetAgentId(payload.What.AttackerAgent, out Guid attackerId) == false) return;

            // next, check if the attacker is one of ours, if not, no networking is needed (not our agent dealing damage)
            if (networkAgentRegistry.IsControlled(attackerId) == false) return;

            if (networkAgentRegistry.TryGetAgentId(payload.What.VictimAgent, out Guid victimId) == false)
            {
                Logger.Warning("Unable to get id for {agent} in {class}", payload.What.VictimAgent, typeof(AgentDamageHandler));
                return;
            };

            // Handles friend fire event
            if (networkAgentRegistry.IsControlled(victimId))
            {
                NetworkAgentDamaged friendlyFireMessage = new NetworkAgentDamaged(
                    attackerId,
                    victimId,
                    payload.What.AttackCollisionData,
                    payload.What.Blow);
                networkMessageBroker.PublishNetworkEvent(friendlyFireMessage);
                return;
            }

                if (networkAgentRegistry.TryGetExternalController(payload.What.VictimAgent, out NetPeer netPeer) == false) return;

            NetworkDamageAgent message = new NetworkDamageAgent(
                attackerId, 
                victimId, 
                payload.What.AttackCollisionData, 
                payload.What.Blow);

            networkMessageBroker.PublishNetworkEvent(netPeer, message);
        }

        private void AgentDamageCheck(MessagePayload<NetworkDamageAgent> payload)
        {
            if (networkAgentRegistry.TryGetAgent(payload.What.VictimAgentId, out Agent resolvedAgent) == false) return;

            if (resolvedAgent.Health <= 0) return;

            NetworkAgentDamaged message = new NetworkAgentDamaged(
                payload.What.AttackerAgentId,
                payload.What.VictimAgentId,
                payload.What.AttackCollisionData,
                payload.What.Blow);

            networkMessageBroker.PublishNetworkEventExcept((NetPeer)payload.Who, message);
            AgentDamagePatch.OverrideAgentDamage(resolvedAgent, payload.What.Blow, payload.What.AttackCollisionData);
        }

        private void AgentDamageRecieve(MessagePayload<NetworkAgentDamaged> payload)
        {
            NetworkAgentDamaged agentDamaData = payload.What;

            NetPeer netPeer = payload.Who as NetPeer;

            Agent effectedAgent = null;
            Agent effectorAgent = null;
            // grab the network registry group controller
            networkAgentRegistry.OtherAgents.TryGetValue(netPeer, out AgentGroupController agentGroupController);

            // start with the attack receiver
            // first check if the receiver of the damage is one the sender's agents
            if (agentGroupController != null && agentGroupController.ControlledAgents.ContainsKey(agentDamaData.VictimAgentId))
            {
                agentGroupController.ControlledAgents.TryGetValue(agentDamaData.VictimAgentId, out effectedAgent);
            }
            // otherwise next, check if it is one of our agents
            else if (networkAgentRegistry.ControlledAgents.ContainsKey(agentDamaData.VictimAgentId))
            {
                networkAgentRegistry.ControlledAgents.TryGetValue(agentDamaData.VictimAgentId, out effectedAgent);
            }
            // now with the attacker
            // check if the attacker is one of the senders (should always be true?)
            if (agentGroupController != null && agentGroupController.ControlledAgents.ContainsKey(agentDamaData.AttackerAgentId))
            {
                agentGroupController.ControlledAgents.TryGetValue(agentDamaData.AttackerAgentId, out effectorAgent);
            }
            else if (networkAgentRegistry.ControlledAgents.ContainsKey(agentDamaData.AttackerAgentId))
            {
                networkAgentRegistry.ControlledAgents.TryGetValue(agentDamaData.AttackerAgentId, out effectorAgent);
            }

            if (effectedAgent == null) return;
            if (effectorAgent == null) return;

            // If agent is already dead return
            if (effectedAgent.Health <= 0) return;

            // extract the blow
            Blow b = agentDamaData.Blow;

            if (b.IsMissile)
            {
                var peerIdx = b.WeaponRecord.AffectorWeaponSlotOrMissileIndex;
                networkMissileRegistry.TryGetIndex(netPeer, peerIdx, out int localIdx);
                b.WeaponRecord.AffectorWeaponSlotOrMissileIndex = localIdx;
            }

            // assign the blow owner from our own index
            b.OwnerId = effectorAgent.Index;

            // extract the collision data
            AttackCollisionData collisionData = agentDamaData.AttackCollisionData;

            GameLoopRunner.RunOnMainThread(() =>
            {
                // register a blow on the effected agent
                effectedAgent.RegisterBlow(b, collisionData);
            });
        }
    }
}