using GameInterface.Services.SiegeEvents;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.BesiegerCamps.Patches;

/// <summary>
/// Vanilla's AI assault roll derefs MobileParty.MainParty, which is null on the dedicated host, so
/// the server's own siege AI would crash the first time it weighed an assault. Same math with the
/// player-inside discount derived from player-led parties instead of the main party.
/// </summary>
[HarmonyPatch(typeof(BesiegerCamp))]
internal class BesiegerCampAssaultPatches
{
    [HarmonyPatch(nameof(BesiegerCamp.StartingAssaultOnBesiegedSettlementIsLogical))]
    [HarmonyPrefix]
    private static bool StartingAssaultIsLogicalPrefix(BesiegerCamp __instance, ref bool __result)
    {
        if (!ContainerProvider.TryResolve<IAiSiegeAssaultReadiness>(out var readiness))
        {
            __result = false;
            return false;
        }

        __result = readiness.ShouldStartAssault(__instance);
        return false;
    }
}
