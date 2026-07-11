using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches;

/// <summary>
/// Refreshes the settlement map visual when its SiegeEvent reference changes. Vanilla dirties the
/// visual in SiegeEventManager.StartSiegeEvent, FinalizeSiegeEvent, and the siege tick, which never
/// run on clients, so the synced setter apply is the only place a client learns the siege camp
/// appeared or went away. The siege/civilian scene level mask is separate from the visual dirty flag;
/// without recalculating it, siege-only slot parents can stay hidden even after preparation completes.
/// </summary>
[HarmonyPatch(typeof(Settlement))]
internal class SiegeEventVisualPatches
{
    [HarmonyPatch(nameof(Settlement.SiegeEvent), MethodType.Setter)]
    [HarmonyPostfix]
    private static void SetSiegeEventPostfix(Settlement __instance)
    {
        __instance.Party?.SetLevelMaskIsDirty();
        __instance.Party?.SetVisualAsDirty();
    }
}
