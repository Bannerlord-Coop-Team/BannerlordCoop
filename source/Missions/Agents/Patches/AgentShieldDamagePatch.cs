using Common.Messaging;
using HarmonyLib;
using Missions.Agents.Extensions;
using Missions.Agents.Messages;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Patches
{
    /// <summary>
    /// Intercept when weapon hitpoints change to send to ShieldDamageHandler (only shields have health)
    /// </summary>
    [HarmonyPatch(typeof(Agent), "OnShieldDamaged")]
    public class ShieldDamagePatch
    {
        private static void Postfix(Agent __instance, EquipmentIndex slotIndex, int inflictedDamage)
        {
            if (__instance.IsLocallyControlled())
                return;

            ShieldDamaged shieldDamage = new ShieldDamaged(__instance, slotIndex, inflictedDamage);
            MessageBroker.Instance.Publish(__instance, shieldDamage);
        }
    }
}
