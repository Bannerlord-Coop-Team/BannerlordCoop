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
        var roster = __instance.ItemRoster;
        if (roster != null)
        {
            ItemRosterLookup.Set(roster, __instance);
        }
    }
}

[HarmonyPatch(typeof(PartyBaseHelper))]
internal class PartyBaseHelperPatch
{
    [HarmonyPatch(nameof(PartyBaseHelper.HasFeat))]
    [HarmonyPrefix]
    public static bool HasFeat(PartyBase party, FeatObject feat, ref bool __result)
    {
        bool hasFeat = false;

        if (party != null)
        {
            if (party.LeaderHero != null && party.LeaderHero.Culture != null)
                hasFeat |= party.LeaderHero.Culture.HasFeat(feat);

            if (party.Culture != null)
                hasFeat |= party.Culture.HasFeat(feat);

            if (party.Owner != null && party.Owner.Culture != null)
                hasFeat |= party.Owner.Culture.HasFeat(feat);

            if (party.Settlement != null && party.Settlement.Culture != null)
                hasFeat |= party.Settlement.Culture.HasFeat(feat);
        }

        __result = hasFeat;
        return false;
    }
}


