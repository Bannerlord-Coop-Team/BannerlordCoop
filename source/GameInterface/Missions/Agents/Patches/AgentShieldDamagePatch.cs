using Common.Messaging;
using GameInterface.Missions.Agents.Messages;
using GameInterface.Missions.Services.Network;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Missions.Agents.Patches
{
    /// <summary>
    /// Intercept when weapon hitpoints change to send to ShieldDamageHandler (only shields have health)
    /// </summary>
    [HarmonyPatch(typeof(Agent), "OnShieldDamaged")]
    public class ShieldDamagePatch
    {
        private static void Postfix(Agent __instance, EquipmentIndex slotIndex, int inflictedDamage)
        {
            if (NetworkAgentRegistry.Instance.IsControlled(__instance) == true) return;
            ShieldDamaged shieldDamage = new ShieldDamaged(__instance, slotIndex, inflictedDamage);
            MessageBroker.Instance.Publish(__instance, shieldDamage);
        }
    }
}
