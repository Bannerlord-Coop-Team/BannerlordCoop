//using GameInterface.Policies;
//using HarmonyLib;
//using TaleWorlds.CampaignSystem.MapEvents;
//using TaleWorlds.CampaignSystem.Party;

//namespace GameInterface.Services.MobileParties.Patches;

//[HarmonyPatch(typeof(PartyBase))]
//class DebugFixPatch2
//{
//    [HarmonyPatch(nameof(PartyBase.MapEventSide), MethodType.Setter)]
//    [HarmonyPrefix]
//    private static bool StringIdPrefix(PartyBase __instance, MapEventSide value)
//    {
//        if (__instance._mapEventSide != value)
//        {
//            if (value != null && __instance.IsMobile && __instance.MapEvent != null && __instance.MapEvent.DefenderSide.LeaderParty == __instance)
//            {
//                ;
//            }
//            if (__instance._mapEventSide != null)
//            {
//                __instance._mapEventSide.RemovePartyInternal(__instance);
//            }
//            __instance._mapEventSide = value;
//            if (__instance._mapEventSide != null)
//            {
//                __instance._mapEventSide.AddPartyInternal(__instance);
//            }
//            if (__instance.MobileParty != null)
//            {
//                if (__instance.IsActive)
//                {
//                    __instance.MobileParty.CancelNavigationTransition();
//                }
//                foreach (MobileParty mobileParty in __instance.MobileParty.AttachedParties)
//                {
//                    mobileParty.Party.MapEventSide = __instance._mapEventSide;
//                }
//            }
//        }

//        return false;
//    }
//}
