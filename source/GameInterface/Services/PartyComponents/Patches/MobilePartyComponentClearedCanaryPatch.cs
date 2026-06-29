using Common.Logging;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.PartyComponents.Patches;

/// <summary>
/// Canary for an assumption the type-flag sync relies on. The client recomputes a party's type
/// flags (IsMilitia/IsCaravan/etc.) from its component only when a component is attached (in
/// PartyComponentHandler). Clearing a component to null instead arrives through the raw
/// _partyComponent field sync, which does not recompute, so the flags would stay stale. Nothing
/// is expected to clear a live party's component at runtime, so this should never fire; if it
/// does, that path needs its own recompute.
/// </summary>
[HarmonyPatch(typeof(MobileParty), nameof(MobileParty.SetPartyComponent))]
internal class MobilePartyComponentClearedCanaryPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<MobilePartyComponentClearedCanaryPatch>();

    [HarmonyPrefix]
    private static void Prefix(MobileParty __instance, PartyComponent partyComponent)
    {
        if (partyComponent != null || __instance._partyComponent == null) return;

        Logger.Error(
            "Party {Party} had its component cleared to null. The client recomputes type flags " +
            "(IsMilitia/IsCaravan/etc.) only when a component is attached, so its flags will stay " +
            "stale after a clear. This was assumed not to happen; the clear path needs a flag recompute.",
            __instance.StringId);
    }
}
