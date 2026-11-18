using Common;
using GameInterface.Services.Ferries.Interfaces;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Ferries.Patches;

[HarmonyPatch(typeof(MobileParty))]
[HarmonyPatchCategory(GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class PlayerFerryStatePatches
{
    // Vanilla only knows the main party's ferry, so the server's IsInNavalAutoTravel checks (sea damage, AI, speed, navigating
    // with no ships) would treat a player on a ferry as an ordinary party at sea
    [HarmonyPatch(nameof(MobileParty.IsInFerryState), MethodType.Getter)]
    [HarmonyPostfix]
    private static void IsInFerryStatePostfix(MobileParty __instance, ref bool __result)
    {
        if (ModInformation.IsClient || __result || !__instance.IsPlayerParty()) return;
        if (!ContainerProvider.TryResolve<IPlayerFerryInterface>(out var ferryInterface)) return;

        __result = ferryInterface.IsOnFerry(__instance);
    }
}
