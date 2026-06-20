using Common.Messaging;
using HarmonyLib;
using Missions.Agents.Messages;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Patches
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
