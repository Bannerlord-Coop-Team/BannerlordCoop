using Common.Messaging;
using HarmonyLib;
using Missions.Agents.Extensions;
using Missions.Missiles.Message;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Missiles.Patches
{
    /// <summary>
    /// MissileAuxPatch to send shoot event and returned resulting integer
    /// </summary>
    [HarmonyPatch(typeof(Mission), "AddMissileAux")]
    public class AddMissileAuxPatch
    {
        private static void Prefix(Agent shooterAgent, ref MissionWeapon __state)
        {
            __state = shooterAgent.WieldedWeapon;
        }
        private static void Postfix(
            int __result,
            Agent shooterAgent,
            ref Vec3 direction,
            ref Vec3 position,
            ref Mat3 orientation,
            float baseSpeed,
            float speed,
            bool addRigidBody,
            ref MissionWeapon __state)
        {
            if (!shooterAgent.IsLocallyControlled())
                return;

            AgentShoot message = new AgentShoot(
                shooterAgent,
                __state,
                position,
                direction,
                orientation,
                baseSpeed,
                speed,
                addRigidBody,
                __result);
            MessageBroker.Instance.Publish(shooterAgent, message);
        }
    }
    /// <summary>
    /// MissileSingleUsageAuxPatch to send shoot event and returned resulting integer
    /// </summary>
    [HarmonyPatch(typeof(Mission), "AddMissileSingleUsageAux")]
    public class AddMissileSingleUsageAuxPatch
    {
        private static void Prefix(Agent shooterAgent, ref MissionWeapon __state)
        {
            __state = shooterAgent.WieldedWeapon;
        }
        private static void Postfix(int __result,
            Agent shooterAgent,
            ref Vec3 direction,
            ref Vec3 position,
            ref Mat3 orientation,
            float baseSpeed,
            float speed,
            bool addRigidBody,
            ref MissionWeapon __state)
        {
            if (!shooterAgent.IsLocallyControlled())
                return;

            AgentShoot message = new AgentShoot(
                shooterAgent,
                __state,
                position,
                direction,
                orientation,
                baseSpeed,
                speed,
                addRigidBody,
                __result);
            MessageBroker.Instance.Publish(shooterAgent, message);
        }
    }
    /// <summary>
    /// Block the locally created missile
    /// </summary>
    [HarmonyPatch(typeof(Mission), "OnAgentShootMissile")]
    public static class BlockMissileIfNative
    {
        [HarmonyPrefix]
        public static bool OnAgentShootMissile(
            ref Agent shooterAgent,
            ref int forcedMissileIndex)
        {
            if (!shooterAgent.IsLocallyControlled() && forcedMissileIndex == -1)
            {
                return false;
            }

            return true;
        }
    }
}
