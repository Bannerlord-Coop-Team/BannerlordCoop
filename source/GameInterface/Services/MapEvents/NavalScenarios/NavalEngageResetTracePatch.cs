#if DEBUG
using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.NavalScenarios;

/// <summary>DEBUG: hands every movement reset on the server to the encounter preset's reset tracer; goes with it once G5 passes live.</summary>
[HarmonyPatch(typeof(MobileParty), nameof(MobileParty.ResetAllMovementParameters))]
internal class NavalEngageResetTracePatch
{
    // Prefix, so the trace shows the behavior and target the reset is about to clear.
    [HarmonyPrefix]
    private static void Prefix(MobileParty __instance)
    {
        if (ModInformation.IsClient) return;
        if (!ContainerProvider.TryResolve<INavalEngageResetTracer>(out var tracer)) return;

        tracer.OnMovementReset(__instance);
    }
}
#endif
