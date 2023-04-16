using Common.Network;
using HarmonyLib;
using Missions.Services.Agents.Messages;
using Missions.Services.Network;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Patches
{
    /// <summary>
    /// Intercept when weapon hitpoints change to send to ShieldDamageHandler (only shields have health)
    /// </summary>
    [HarmonyPatch(typeof(Agent), "OnShieldDamage")]
    public class ShieldDamagePatch
    {
        private static void Postfix(Agent __instance, EquipmentIndex slotIndex, int inflictedDamage)
        {
            if (NetworkAgentRegistry.Instance.IsControlled(__instance) == false) return;
            ShieldDamaged shieldDamage = new ShieldDamaged(__instance, slotIndex, inflictedDamage);
            NetworkMessageBroker.Instance.Publish(__instance, shieldDamage);
        }
    }
}
