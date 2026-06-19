using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Missions.Agents.Extensions;
using GameInterface.Missions.Agents.Messages;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Missions.Agents.Patches
{
    /// <summary>
    /// Intercept agent damage and determine if a network call is needed
    /// </summary>
    [HarmonyPatch(typeof(Mission), "RegisterBlow")]
    public class AgentDamagePatch
    {
        private static bool Prefix(Agent attacker, Agent victim, Blow b, ref AttackCollisionData collisionData)
        {
            if (!attacker.IsLocallyControlled()) return false;

            // construct a agent damage data
            AgentDamaged agentDamageData = new AgentDamaged(attacker, victim, b, collisionData);

            // publish the event
            MessageBroker.Instance.Publish(attacker, agentDamageData);

            if (victim.IsLocallyControlled()) return true;
             
            return false;
        }
    }

    [HarmonyPatch(typeof(Agent), "RegisterBlow")]
    public class RegisterBlowPatch
    {
        private static bool Prefix(ref Agent __instance)
        {
            if (AllowedThread.IsThisThreadAllowed()) return true;

            return __instance.IsLocallyControlled();
        }

        public static void RunOriginalRegisterBlow(Agent agent, Blow blow, AttackCollisionData collisionData)
        {
            GameThread.Run(() =>
            {
                using(new AllowedThread())
                {
                    agent.RegisterBlow(blow, collisionData);
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
            return attacker.IsLocallyControlled();
        }
    }

    [HarmonyPatch(typeof(Mission), "FallDamageCallback")]
    public class FallDamageCallbackPatch
    {
        private static bool Prefix(ref Agent attacker)
        {
            return attacker.IsLocallyControlled();
        }
    }

    [HarmonyPatch(typeof(Mission), "MeleeHitCallback")]
    public class MeleeHitCallbackPatch
    {
        private static bool Prefix(ref Agent attacker)
        {
            return attacker.IsLocallyControlled();
        }
    }

    [HarmonyPatch(typeof(Mission), "MissileAreaDamageCallback")]
    public class MissileAreaDamageCallbackPatch
    {
        private static bool Prefix(ref Agent shooterAgent)
        {
            return shooterAgent.IsLocallyControlled();
        }
    }

    [HarmonyPatch(typeof(Mission), "MissileHitCallback")]
    public class MissileHitCallbackPatch
    {
        private static bool Prefix(ref Agent attacker)
        {
            return attacker.IsLocallyControlled();
        }
    }
    #endregion
}