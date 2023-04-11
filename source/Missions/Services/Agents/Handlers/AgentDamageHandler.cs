using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Serialization;
using LiteNetLib;
using Missions.Services.Agents.Messages;
using Missions.Services.Agents.Packets;
using Missions.Services.Network;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Handlers
{
    internal interface IAgentDamageHandler : IHandler
    {
        void AgentDamageSend(MessagePayload<AgentDamage> payload);
        void AgentDamageRecieve(MessagePayload<NetworkAgentDamage> payload);
    }
    public class AgentDamageHandler : IAgentDamageHandler
    {
        readonly NetworkAgentRegistry networkAgentRegistry;
        readonly NetworkMessageBroker networkMessageBroker;
        public AgentDamageHandler(NetworkAgentRegistry networkAgentRegistry, NetworkMessageBroker networkMessageBroker) 
        {
            this.networkAgentRegistry = networkAgentRegistry;
            this.networkMessageBroker = networkMessageBroker;

            networkMessageBroker.Subscribe<AgentDamage>(AgentDamageSend);
            networkMessageBroker.Subscribe<NetworkAgentDamage>(AgentDamageRecieve);
        }
        ~AgentDamageHandler()
        {
            networkMessageBroker.Unsubscribe<AgentDamage>(AgentDamageSend);
            networkMessageBroker.Unsubscribe<NetworkAgentDamage>(AgentDamageRecieve);
        }

        public void AgentDamageSend(MessagePayload<AgentDamage> payload)
        {
            // first, check if the attacker exists in the agent to ID groud, if not, no networking is needed (not a network agent)
            if (NetworkAgentRegistry.Instance.TryGetAgentId(payload.What.AttackerAgent, out Guid attackerId) == false) return;

            // next, check if the attacker is one of ours, if not, no networking is needed (not our agent dealing damage)
            if (NetworkAgentRegistry.Instance.IsControlled(attackerId) == false) return;

            // If there is package factory cannot be resolved, do default behavior
            if (ContainerProvider.TryResolve<IBinaryPackageFactory>(out var packageFactory) == false) return;

            networkAgentRegistry.TryGetAgentId(payload.What.VictimAgent, out Guid victimId);

            NetworkAgentDamage message = new NetworkAgentDamage(attackerId, victimId, payload.What.AttackCollisionData, payload.What.Blow);

            networkMessageBroker.PublishNetworkEvent(message);
        }

        public void AgentDamageRecieve(MessagePayload<NetworkAgentDamage> payload)
        {
            NetworkAgentDamage agentDamaData = payload.What;

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

            // extract the blow
            Blow b = agentDamaData.Blow;

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