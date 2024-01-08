using Common.Messaging;
using Common.Network;
using HarmonyLib;
using Missions.Services.Agents.Messages;
using Missions.Services.Network;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Patches
{
    /// <summary>
    /// Patch on DropItem for WeaponDropHandler
    /// </summary>
    [HarmonyPatch(typeof(Agent), "DropItem")]
    public class AgentDropPatch
    {
        static void Postfix(EquipmentIndex itemIndex, Agent __instance)
        {
            WeaponDropped message = new WeaponDropped(__instance, itemIndex);
            MessageBroker.Instance.Publish(__instance, message);
        }
    }
}
