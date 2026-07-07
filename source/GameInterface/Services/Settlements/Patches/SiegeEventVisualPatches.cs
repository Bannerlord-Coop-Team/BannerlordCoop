using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Patches;

/// <summary>
/// Refreshes the settlement map visual when its SiegeEvent reference changes. Vanilla dirties the
/// visual in SiegeEventManager.StartSiegeEvent and the siege tick, which never run on clients, so the
/// synced setter apply is the only place a client learns the siege camp appeared or went away.
/// </summary>
[HarmonyPatch(typeof(Settlement))]
internal class SiegeEventVisualPatches
{
    [HarmonyPatch(nameof(Settlement.SiegeEvent), MethodType.Setter)]
    [HarmonyPostfix]
    private static void SetSiegeEventPostfix(Settlement __instance)
    {
        __instance.Party?.SetVisualAsDirty();
    }
}
