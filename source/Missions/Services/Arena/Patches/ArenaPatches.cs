using Common.Network;
using HarmonyLib;
using Missions.Services.Agents.Packets;
using Missions.Services.Network;
using System;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Arena.Patches
{
    [HarmonyPatch(typeof(Mission), "RegisterBlow")]
    public class AgentDamagePatch
    {
        static bool Prefix(Agent attacker, Agent victim, GameEntity realHitEntity, Blow b, ref AttackCollisionData collisionData, in MissionWeapon attackerWeapon, ref CombatLogData combatLogData)
        {
            if (!NetworkAgentRegistry.Instance.AgentToId.TryGetValue(attacker, out Guid attackerId)) return true;
            if (!NetworkAgentRegistry.Instance.ControlledAgents.ContainsKey(attackerId)) return true;
            AgentDamageData _agentDamageData;
            NetworkAgentRegistry.Instance.AgentToId.TryGetValue(victim, out Guid victimId);
            _agentDamageData = new AgentDamageData(attackerId, victimId, b.InflictedDamage);
            NetworkMessageBroker.Instance.PublishNetworkEvent(_agentDamageData);

            return true;
        }
    }
}
