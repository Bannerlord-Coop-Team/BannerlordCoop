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
        private static void Prefix(Agent attacker, Agent victim, Blow b, ref AttackCollisionData collisionData)
        {
            // construct a agent damage data
            AgentDamage agentDamageData = new AgentDamage(attacker, victim, b, collisionData);

            // publish the event
            MessageBroker.Instance.Publish(attacker, agentDamageData);
        }
    }
    /// <summary>
    /// Only allow damage from controlled agents
    /// </summary>
    [HarmonyPatch(typeof(Mission), "OnAgentHit")]
    internal static class OnAgentHitPatch
    {
        private static bool Prefix(ref Agent affectorAgent)
        {
            if (!NetworkAgentRegistry.Instance.IsControlled(affectorAgent)) return false;

            return true;
        }
    }
    /// <summary>
    /// Intercept when weapon hitpoints change to send to ShieldDamageHandler (only shields have health)
    /// </summary>
    [HarmonyPatch(typeof(Agent), nameof(Agent.ChangeWeaponHitPoints))]
    public class ShieldDamagePatch
    {
        private static void Postfix(Agent __instance, EquipmentIndex slotIndex, short hitPoints)
        {
            ShieldDamaged shieldDamage = new ShieldDamaged(__instance, slotIndex, hitPoints);
            NetworkMessageBroker.Instance.Publish(__instance, shieldDamage);
        }
    }
}