using Common;
using Common.Messaging;
using Common.Util;
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
        private static bool Prefix(Agent attacker, Agent victim, Blow b, ref AttackCollisionData collisionData)
        {
            if (NetworkAgentRegistry.Instance.IsControlled(attacker) == false) return false;

            // construct a agent damage data
            AgentDamaged agentDamageData = new AgentDamaged(attacker, victim, b, collisionData);

            // publish the event
            MessageBroker.Instance.Publish(attacker, agentDamageData);

            if (NetworkAgentRegistry.Instance.IsControlled(victim)) return true;
             
            return false;
        }
    }

    [HarmonyPatch(typeof(Agent), "RegisterBlow")]
    public class RegisterBlowPatch
    {
        private static AllowedInstance<Agent> _allowedInstance;

        private static bool Prefix(ref Agent __instance)
        {
            if (__instance == _allowedInstance?.Instance) return true;

            return NetworkAgentRegistry.Instance.IsControlled(__instance);
        }

        public static void RunOriginalRegisterBlow(Agent agent, Blow blow, AttackCollisionData collisionData)
        {
            using(_allowedInstance = new AllowedInstance<Agent>())
            {
                GameLoopRunner.RunOnMainThread(() =>
                {
                    agent.RegisterBlow(blow, collisionData);
                }, true);
            }
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