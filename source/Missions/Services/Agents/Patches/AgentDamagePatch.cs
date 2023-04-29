using Common;
using Common.Messaging;
using HarmonyLib;
using Missions.Services.Agents.Messages;
using Missions.Services.Network;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Patches
{
    /// <summary>
    /// Intercept agent damage and determine if a network call is needed
    /// </summary>
    [HarmonyPatch(typeof(Mission), "RegisterBlow")]
    public class AgentDamagePatch
    {
        private static Agent _applyDamageAgent;

        private static void Prefix(Agent attacker, Agent victim, Blow b, ref AttackCollisionData collisionData)
        {
            if(_applyDamageAgent == victim) return;

            if (NetworkAgentRegistry.Instance.IsControlled(attacker) == false) return;

            // construct a agent damage data
            AgentDamaged agentDamageData = new AgentDamaged(attacker, victim, b, collisionData);

            // publish the event
            MessageBroker.Instance.Publish(attacker, agentDamageData);
        }

        public static void OverrideAgentDamage(Agent victim, Blow blow, AttackCollisionData collisionData)
        {
            GameLoopRunner.RunOnMainThread(() =>
            {
                lock (_applyDamageAgent)
                {
                    _applyDamageAgent = victim;
                    _applyDamageAgent.RegisterBlow(blow, collisionData);
                }
            });
        }
    }

    #region SkipPatches
    [HarmonyPatch(typeof(Mission), "ChargeDamageCallback")]
    public class ChargeDamageCallbackPatch
    {
        private static bool Prefix(ref Agent attacker)
        {
            return NetworkAgentRegistry.Instance.IsControlled(attacker);
        }
    }

    [HarmonyPatch(typeof(Mission), "FallDamageCallback")]
    public class FallDamageCallbackPatch
    {
        private static bool Prefix(ref Agent attacker)
        {
            return NetworkAgentRegistry.Instance.IsControlled(attacker);
        }
    }

    [HarmonyPatch(typeof(Mission), "MeleeHitCallback")]
    public class MeleeHitCallbackPatch
    {
        private static bool Prefix(ref Agent attacker)
        {
            return NetworkAgentRegistry.Instance.IsControlled(attacker);
        }
    }

    [HarmonyPatch(typeof(Mission), "MissileAreaDamageCallback")]
    public class MissileAreaDamageCallbackPatch
    {
        private static bool Prefix(ref Agent shooterAgent)
        {
            return NetworkAgentRegistry.Instance.IsControlled(shooterAgent);
        }
    }

    [HarmonyPatch(typeof(Mission), "MissileHitCallback")]
    public class MissileHitCallbackPatch
    {
        private static bool Prefix(ref Agent attacker)
        {
            return NetworkAgentRegistry.Instance.IsControlled(attacker);
        }
    }
    #endregion
}