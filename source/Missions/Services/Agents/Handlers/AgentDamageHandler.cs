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
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using static TaleWorlds.CampaignSystem.CharacterDevelopment.DefaultPerks;

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
            networkMessageBroker.Subscribe<NetworkAgentKilled>(Handle_NetworkAgentKilled);
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
            networkMessageBroker.Unsubscribe<NetworkAgentKilled>(Handle_NetworkAgentKilled);
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

            Logger.Debug("Damage Check Sent to " + victimId + " for: " + payload.What.Blow.InflictedDamage);

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

            if (networkAgentRegistry.TryGetExternalController(payload.What.VictimAgent, out NetPeer netPeer) == false)
            {
                Logger.Error("Could not get ExternalController for " + payload.What.VictimAgent.Name);
                return;
            }

            NetworkDamageAgent message = new NetworkDamageAgent(
                attackerId, 
                victimId, 
                payload.What.AttackCollisionData, 
                payload.What.Blow);

            networkMessageBroker.PublishNetworkEvent(netPeer, message);
        }

        private void AgentDamageCheck(MessagePayload<NetworkDamageAgent> payload)
        {
            if (networkAgentRegistry.TryGetAgent(payload.What.AttackerAgentId, out Agent attackingAgent) == false) return;

            var message = payload.What;

            var blow = message.Blow;
            blow.OwnerId = attackingAgent.Index;

            NetworkAgentDamaged damageMessage = new NetworkAgentDamaged(
                payload.What.AttackerAgentId,
                payload.What.VictimAgentId,
                payload.What.AttackCollisionData,
                payload.What.Blow);

            Logger.Debug("Damage Check Recieved from " + payload.What.AttackerAgentId + " for: " + payload.What.Blow.InflictedDamage);

            if (networkAgentRegistry.TryGetAgent(payload.What.VictimAgentId, out Agent victimAgent) == false) return;

            GameLoopRunner.RunOnMainThread(() =>
            {
                try
                {
                    victimAgent.RegisterBlow(blow, message.AttackCollisionData);
                }
                catch(Exception e)
                {
                    victimAgent.Health -= blow.InflictedDamage;
                    Logger.Error("Exception on victimAgent register blow, most likely related to missile index " + e.Message);
                }
            }, true);

            if (victimAgent.Health <= 0)
            {
                var killedMessage = new NetworkAgentKilled(
                    payload.What.VictimAgentId,
                    payload.What.AttackerAgentId,
                    payload.What.Blow);

                Logger.Verbose($"Sending agent killed for {victimAgent.Name}");

                //networkMessageBroker.PublishNetworkEvent(killedMessage);
                //return;
            }

            networkMessageBroker.PublishNetworkEvent(damageMessage);
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

            if (effectedAgent == null)
            {
                Logger.Error("Could not find victim agent");
                return;
            }

            if (effectorAgent == null)
            {
                Logger.Error("Could not find attacker agent");
                return;
            }

            // If agent is already dead return
            if (effectedAgent.Health <= 0)
            {
                Logger.Error("Attempted to damage agent that was already dead");
                return;
            }

            // extract the blow
            Blow blow = agentDamaData.Blow;

            if (blow.IsMissile)
            {
                var peerIdx = blow.WeaponRecord.AffectorWeaponSlotOrMissileIndex;

                if(networkMissileRegistry.TryGetIndex(netPeer, peerIdx, out int localIdx) == false)
                {
                    Logger.Error($"Missile did not exist in registry, idx: {peerIdx}, number of peers: {networkMissileRegistry.Length}");
                    effectedAgent.Health -= blow.InflictedDamage;
                    return;
                };

                blow.WeaponRecord.AffectorWeaponSlotOrMissileIndex = localIdx;
            }

            // assign the blow owner from our own index
            blow.OwnerId = effectorAgent.Index;

            // extract the collision data
            AttackCollisionData collisionData = agentDamaData.AttackCollisionData;

            Logger.Information("Damage recieved to " + agentDamaData.VictimAgentId + " for: " + blow.InflictedDamage);

            // register a blow on the effected agent
            RegisterBlowPatch.RunOriginalRegisterBlow(effectedAgent, blow, collisionData);
        }

        private void Handle_NetworkAgentKilled(MessagePayload<NetworkAgentKilled> obj)
        {
            if(networkAgentRegistry.TryGetAgent(obj.What.VictimAgentId, out var agent) &&
               networkAgentRegistry.TryGetAgent(obj.What.AttackingAgentId, out var attackingAgent))
            {
                if (agent.Health <= 0) return;

                Logger.Verbose($"Handling agent killed for {agent.Name} by {attackingAgent.Name}");

                Blow blow = obj.What.Blow;

                blow.OwnerId = attackingAgent.Index;

                Agent.KillInfo overrideKillInfo = blow.IsFallDamage ? Agent.KillInfo.Gravity : Agent.KillInfo.Invalid;

                GameLoopRunner.RunOnMainThread(() =>
                {
                    agent.Die(blow, overrideKillInfo);
                }, true);
            }
        }
    }
}