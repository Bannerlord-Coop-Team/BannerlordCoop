using Common.Messaging;
using Common.Network;
using HarmonyLib;
using JetBrains.Annotations;
using Missions.Services.Missiles.Message;
using Missions.Services.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Missiles.Patches
{
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
            if (NetworkAgentRegistry.Instance.IsControlled(shooterAgent))
            {
                AgentShoot message = new AgentShoot(shooterAgent, __state, position, direction, orientation, baseSpeed, speed, addRigidBody, __result);
                NetworkMessageBroker.Instance.Publish(shooterAgent, message);
            }
        }
    }

    [HarmonyPatch(typeof(Mission), "AddMissileSingleUsageAux")]
    public class AddMissileSingleUsageAuxPatch
    {
        private static void Prefix(Agent shooterAgent, ref MissionWeapon __state)
        {
            __state = shooterAgent.WieldedWeapon;
        }
        private static void Postfix(int __result, Agent shooterAgent, ref Vec3 direction, ref Vec3 position, ref Mat3 orientation, float baseSpeed, float speed, bool addRigidBody, int forcedMissileIndex, ref MissionWeapon __state)
        {
            if (NetworkAgentRegistry.Instance.IsControlled(shooterAgent))
            {
                AgentShoot message = new AgentShoot(shooterAgent, __state, position, direction, orientation, baseSpeed, speed, addRigidBody, __result);
                NetworkMessageBroker.Instance.Publish(shooterAgent, message);
            }
        }
    }

    [HarmonyPatch(typeof(Mission), "OnAgentShootMissile")]
    public static class BlockMissileIfNative
    {
        [HarmonyPrefix]
        public static bool OnAgentShootMissile(
            ref Agent shooterAgent,
            ref int forcedMissileIndex)
        {
            if (!NetworkAgentRegistry.Instance.IsControlled(shooterAgent) && forcedMissileIndex == -1)
            {
                return false;
            }

            return true;
        }
    }
}
