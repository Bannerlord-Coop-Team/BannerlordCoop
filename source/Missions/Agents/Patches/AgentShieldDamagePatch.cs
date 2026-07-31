using Common.Messaging;
using Common.Util;
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
    [HarmonyPatchCategory(MissionModule.ShieldDamagePatchCategory)]
    public class ShieldDamagePatch
    {
        private static void Postfix(Agent __instance, EquipmentIndex slotIndex, int inflictedDamage)
        {
            if (AllowedThread.IsThisThreadAllowed() ||
                Mission.Current?.GetMissionBehavior<CoopMissionController>() == null ||
                __instance.IsLocallyControlled())
                return;

            ShieldDamaged shieldDamage = new ShieldDamaged(__instance, slotIndex, inflictedDamage);
            MessageBroker.Instance.Publish(__instance, shieldDamage);
        }
    }
}
