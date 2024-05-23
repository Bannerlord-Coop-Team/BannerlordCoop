//using HarmonyLib;
//using System;
//using System.Collections.Generic;
//using System.Reflection;
//using System.Text;
//using TaleWorlds.CampaignSystem.Party;
//using Common.Extensions;

//namespace GameInterface.Services.GameDebug.Patches
//{
//    /// <summary>
//    /// Patches for party visibility
//    /// </summary>
//    [HarmonyPatch(typeof(MobileParty))]
//    internal class PartyVisibilityPatch
//    {
//        public static bool AllPartiesVisible = false;


//        [HarmonyPrefix]
//        [HarmonyPatch(nameof(MobileParty.IsVisible), MethodType.Setter)]
//        private static bool IsVisibleSetter(ref MobileParty __instance)
//        {
//            if (AllPartiesVisible == false) return true;

//            __instance.IsVisible = true;
//            __instance.Party.OnVisibilityChanged(true);

//            return false;
//        }

//        [HarmonyPostfix]
//        [HarmonyPatch(nameof(MobileParty.IsVisible), MethodType.Getter)]
//        private static void IsVisibleGetter(ref bool __result)
//        {
//            if (AllPartiesVisible)
//            {
//                __result = true;
//                return;
//            }
//        }
//    }
//}
