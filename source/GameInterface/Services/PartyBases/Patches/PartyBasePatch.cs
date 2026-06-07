using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using GameInterface.Policies;

namespace GameInterface.Services.PartyBases.Patches;

[HarmonyPatch(typeof(PartyBaseHelper))]
internal class PartyBaseHelperPatch
{
    [HarmonyPatch(nameof(PartyBaseHelper.HasFeat))]
    [HarmonyPrefix]
    public static bool HasFeat(PartyBase party, FeatObject feat, ref bool __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        if (party == null)
            __result = false;
        if (party.LeaderHero != null)
            __result = party.LeaderHero.Culture.HasFeat(feat);
        if (party.MapFaction?.Culture != null)
            __result = party.Culture.HasFeat(feat);
        if (party.Owner?.Culture != null)
            __result = party.Owner.Culture.HasFeat(feat);
        __result = party.Settlement != null && party.Settlement.Culture.HasFeat(feat);

        return false;
    }
}
