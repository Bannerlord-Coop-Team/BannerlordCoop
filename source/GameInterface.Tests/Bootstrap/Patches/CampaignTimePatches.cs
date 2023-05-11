using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Tests.Bootstrap.Patches
{
    [HarmonyPatch(typeof(CampaignTime))]
    internal class CampaignTimePatches
    {
        [HarmonyPatch(nameof(CampaignTime.HoursFromNow))]
        [HarmonyPrefix]
        private static bool AddEventHandlersPrefix()
        {
            return false;
        }

        [HarmonyPatch(nameof(CampaignTime.HoursFromNow))]
        [HarmonyPostfix]
        private static void AddEventHandlersPostfix(ref CampaignTime __result)
        {
            __result = CampaignTime.Zero;
        }
    }
}
