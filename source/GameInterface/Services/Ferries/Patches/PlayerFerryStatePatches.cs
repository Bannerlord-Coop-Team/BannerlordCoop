using GameInterface.Services.Ferries.Interfaces;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Ferries.Patches;

[HarmonyPatch(typeof(MobileParty))]
[HarmonyPatchCategory(GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class PlayerFerryStatePatches
{
    // Vanilla only knows the main party's ferry, so the IsInNavalAutoTravel checks (sea damage, AI, speed, navigating with no
    // ships, the ferry visual) would treat another player on a ferry as an ordinary party at sea. The main party keeps vanilla's.
    [HarmonyPatch(nameof(MobileParty.IsInFerryState), MethodType.Getter)]
    [HarmonyPostfix]
    private static void IsInFerryStatePostfix(MobileParty __instance, ref bool __result)
    {
        if (__result || __instance.IsMainParty || !__instance.IsCurrentlyAtSea || !__instance.IsPlayerParty()) return;
        if (!ContainerProvider.TryResolve<IPlayerFerryInterface>(out var ferryInterface)) return;

        __result = ferryInterface.IsOnFerry(__instance);
    }
}
