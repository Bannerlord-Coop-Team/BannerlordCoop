using GameInterface.Services.SiegeEvents;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.BesiegerCamps.Patches;

/// <summary>
/// Replaces the vanilla AI assault roll on the dedicated host, which has no main party.
/// Co-op derives the defender discount from player-led parties and caps the unfinished-equipment penalty.
/// </summary>
[HarmonyPatch(typeof(BesiegerCamp))]
internal class BesiegerCampAssaultPatches
{
    private static readonly IAiSiegeAssaultReadiness fallbackReadiness = new AiSiegeAssaultReadiness();

    [HarmonyPatch(nameof(BesiegerCamp.StartingAssaultOnBesiegedSettlementIsLogical))]
    [HarmonyPrefix]
    private static bool StartingAssaultIsLogicalPrefix(BesiegerCamp __instance, ref bool __result)
    {
        __result = ResolveReadiness().ShouldStartAssault(__instance);
        return false;
    }

    internal static IAiSiegeAssaultReadiness ResolveReadiness()
    {
        return ContainerProvider.TryResolve<IAiSiegeAssaultReadiness>(out var readiness)
            ? readiness
            : fallbackReadiness;
    }
}
