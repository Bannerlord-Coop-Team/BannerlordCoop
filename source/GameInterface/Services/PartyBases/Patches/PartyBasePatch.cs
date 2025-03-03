using GameInterface.Services.ItemRosters;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
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


