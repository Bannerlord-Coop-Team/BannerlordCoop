using GameInterface;
using GameInterface.Services.Clans;
using HarmonyLib;
using NavalDLC.GameComponents;
using TaleWorlds.CampaignSystem;

namespace Coop.Naval.Patches;

[HarmonyPatch(typeof(NavalDLCClanPoliticsModel))]
internal class NavalDLCClanPoliticsModelPatches
{
    // The default influence tick is gated for player clans, so also skip the Naval Conjoining Statute bonus
    [HarmonyPatch(nameof(NavalDLCClanPoliticsModel.CalculateInfluenceChange))]
    [HarmonyPrefix]
    private static bool CalculateInfluenceChangePrefix(NavalDLCClanPoliticsModel __instance, ref ExplainedNumber __result, Clan clan, bool includeDescriptions)
    {
        // AI led clans tick normally, player led clans use the same gate as the gold tick
        if (clan.Leader == null) return true;

        ContainerProvider.TryResolve<IClanFinance>(out var finance);

        if (finance.CanChangeGold(clan.Leader)) return true;

        __result = __instance.BaseModel.CalculateInfluenceChange(clan, includeDescriptions);
        return false;
    }
}
