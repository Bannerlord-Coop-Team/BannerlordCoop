using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.PartyBases.Patches;

[HarmonyPatch(typeof(PartyBaseHelper))]
internal class PartyBaseHelperPatch
{
    [HarmonyPatch(nameof(PartyBaseHelper.HasFeat))]
    [HarmonyPrefix]
    public static bool HasFeat(PartyBase party, FeatObject feat, ref bool __result)
    {
        __result = (party?.LeaderHero?.Culture?.HasFeat(feat) == true)
            || (party?.MapFaction?.Culture?.HasFeat(feat) == true)
            || (party?.Owner?.Culture?.HasFeat(feat) == true)
            || (party?.Settlement?.Culture?.HasFeat(feat) == true);

        return false;
    }
}
