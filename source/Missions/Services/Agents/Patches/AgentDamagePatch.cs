using Common.Messaging;
using Common.Network;
using GameInterface.Serialization;
using HarmonyLib;
using Missions.Services.Agents.Messages;
using Missions.Services.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
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
            // construct a agent damage data
            AgentDamage agentDamageData = new AgentDamage(attacker, victim, b, collisionData);

            // publish the event
            MessageBroker.Instance.Publish(attacker, agentDamageData);

            return true;
        }
    }
    [HarmonyPatch(typeof(Mission), "OnAgentHit")]
    internal static class OnAgentHitPatch
    {
        private static bool Prefix(ref Agent affectorAgent)
        {
            // Only allow damage from controlled agents
            if (!NetworkAgentRegistry.Instance.IsControlled(affectorAgent)) return false;

            return true;
        }
    }
    [HarmonyPatch(typeof(Agent), nameof(Agent.ChangeWeaponHitPoints))]
    public class ShieldDamagePatch
    {
        static void Postfix(Agent __instance, EquipmentIndex slotIndex, short hitPoints)
        {
            if (NetworkAgentRegistry.Instance.TryGetAgentId(__instance, out Guid agentId) == false) return;

            if (NetworkAgentRegistry.Instance.IsControlled(agentId) == false) return;

            if (hitPoints <= 0) 
            {
                ShieldBreak shieldDamage = new ShieldBreak(__instance, slotIndex);
                NetworkMessageBroker.Instance.Publish(__instance, shieldDamage);
            }
        }
    }
}
