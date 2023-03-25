using Common.Network;
using HarmonyLib;
using Missions.Services.Agents.Messages;
using Missions.Services.Network;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Patches
{
    [HarmonyPatch(typeof(Agent), "DropItem")]
    public class AgentDropPatch
    {
        static void Postfix(Agent __instance, EquipmentIndex equipmentIndex)
        {
            if (NetworkAgentRegistry.Instance.IsControlled(__instance)) 
            {
                WeaponDropped message = new WeaponDropped(__instance, equipmentIndex);
                NetworkMessageBroker.Instance.Publish(__instance, message);
            }
        }
    }
}
