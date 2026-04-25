using Common;
using GameInterface.Policies;
using GameInterface.Services.ItemRosters;
using HarmonyLib;
using Helpers;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyBases.Patches;

[HarmonyPatch(typeof(PartyBase))]
internal class PartyBasePatch
{
    [HarmonyPatch(nameof(PartyBase.ItemRoster), MethodType.Setter)]
    [HarmonyPostfix]
    public static void ItemRosterSetterPostfix(PartyBase __instance)
    {
        if (ModInformation.IsClient) return;

        ItemRosterLookup.Set(__instance.ItemRoster, __instance);
    }

    [HarmonyPatch(nameof(PartyBase.MapEventSide), MethodType.Setter)]
    [HarmonyPrefix]
    public static bool Prefixtest(PartyBase __instance, MapEventSide value)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        if (__instance._mapEventSide != value)
        {
            if (__instance._mapEventSide != null)
            {
                __instance._mapEventSide.RemovePartyInternal(__instance);
            }
            __instance._mapEventSide = value;
            if (__instance._mapEventSide != null)
            {
                __instance._mapEventSide.AddPartyInternal(__instance);
            }
            if (__instance.MobileParty != null)
            {
                if (__instance.IsActive)
                {
                    __instance.MobileParty.CancelNavigationTransition();
                }
                foreach (MobileParty mobileParty in __instance.MobileParty.AttachedParties)
                {
                    mobileParty.Party.MapEventSide = __instance._mapEventSide;
                }
            }
        }

        return false;
    }
}

[HarmonyPatch(typeof(PartyBaseHelper))]
internal class PartyBaseHelperPatch
{
    [HarmonyPatch(nameof(PartyBaseHelper.HasFeat))]
    [HarmonyPrefix]
    public static bool HasFeat(PartyBase party, FeatObject feat, ref bool __result)
    {
        if (party == null)
            __result = false;
        if (party.LeaderHero != null)
            __result = party.LeaderHero.Culture.HasFeat(feat);
        if (party.Culture != null)
            __result = party.Culture.HasFeat(feat);
        if (party.Owner != null)
            __result = party.Owner.Culture.HasFeat(feat);
        __result = party.Settlement != null && party.Settlement.Culture.HasFeat(feat);

        return false;
    }
}


