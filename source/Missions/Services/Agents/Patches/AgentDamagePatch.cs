using Common.Network;
using GameInterface.Serialization;
using HarmonyLib;
using Missions.Services.Agents.Messages;
using Missions.Services.Network;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Patches
{
    /// <summary>
    /// Intercept agent damage and determine if a network call is needed
    /// </summary>
    [HarmonyPatch(typeof(Mission), "RegisterBlow")]
    public class AgentDamagePatch
    {
        static bool Prefix(Agent attacker, Agent victim, Blow b, ref AttackCollisionData collisionData)
        {
            // first, check if the attacker exists in the agent to ID groud, if not, no networking is needed (not a network agent)
            if (NetworkAgentRegistry.Instance.TryGetAgentId(attacker, out Guid attackerId) == false) return true;

            // next, check if the attacker is one of ours, if not, no networking is needed (not our agent dealing damage)
            if (NetworkAgentRegistry.Instance.IsControlled(attackerId) == false) return true;

            // If there is package factory cannot be resolved, do default behavior
            if (ContainerProvider.TryResolve<IBinaryPackageFactory>(out var packageFactory) == false) return true;

            // get the victim GUI
            NetworkAgentRegistry.Instance.AgentToId.TryGetValue(victim, out Guid victimId);

            // construct a agent damage data
            AgentDamageData agentDamageData = new AgentDamageData(attackerId, victimId, collisionData, b);

            // publish the event
            NetworkMessageBroker.Instance.PublishNetworkEvent(agentDamageData);

            return true;
        }
    }
}
