using Common.Messaging;
using HarmonyLib;
using Missions.Services.Agents.Messages;
using NetworkMessages.FromServer;
using SandBox.Conversation.MissionLogics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using static TaleWorlds.MountAndBlade.Agent;

namespace Missions.Services.Agents.Patches
{
    [HarmonyPatch(typeof(Mission), "OnAgentShootMissile")]
    public class OnAgentShootMissilePatch
    {
        public static void Postfix(Agent shooterAgent, EquipmentIndex weaponIndex, Vec3 position, Vec3 velocity,
            Mat3 orientation, bool hasRigidBody, int forcedMissileIndex)
        {
            AgentShoot message = new AgentShoot(shooterAgent, weaponIndex, position, velocity, orientation, hasRigidBody, forcedMissileIndex);

            MessageBroker.Instance.Publish(shooterAgent, message);
        }
    }
}
