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
        static void Postfix(EquipmentIndex itemIndex, WeaponClass pickedUpItemType, Agent __instance)
        {
            if (NetworkAgentRegistry.Instance.IsControlled(__instance)) 
            {
                WeaponDropped message = new WeaponDropped(__instance, itemIndex);
                NetworkMessageBroker.Instance.Publish(__instance, message);
            }
        }
    }
}
