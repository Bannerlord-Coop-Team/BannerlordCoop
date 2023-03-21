using Common.Messaging;
using Common.Network;
using HarmonyLib;
using Missions.Services.Missiles.Message;
using Missions.Services.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Missiles.Patches
{
    [HarmonyPatch(typeof(Mission), "AddMissileAux")]
    public class AddMissileAuxPatch
    {
        private static void Postfix(int __result, Agent shooterAgent, ref Vec3 direction, ref Vec3 position, ref Mat3 orientation, bool addRigidBody, int forcedMissileIndex)
        {
            if (NetworkAgentRegistry.Instance.IsControlled(shooterAgent))
            {
                NetworkMessageBroker.Instance.Publish(shooterAgent, new AgentShoot(shooterAgent, position, direction, orientation, addRigidBody, forcedMissileIndex, __result));
                InformationManager.DisplayMessage(new InformationMessage("MissileAux"));
            }
        }
    }

    [HarmonyPatch(typeof(Mission), "AddMissileSingleUsageAux")]
    public class AddMissileSingleUsageAuxPatch
    {
        private static void Postfix(int __result, Agent shooterAgent, ref Vec3 direction, ref Vec3 position, ref Mat3 orientation, bool addRigidBody, int forcedMissileIndex)
        {
            if (NetworkAgentRegistry.Instance.IsControlled(shooterAgent))
            {
                NetworkMessageBroker.Instance.Publish(shooterAgent, new AgentShoot(shooterAgent, position, direction, orientation, addRigidBody, forcedMissileIndex, __result));
                InformationManager.DisplayMessage(new InformationMessage("SingleMissileAux"));
            }
        }
    }
}
