using Common;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.Rafts.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Rafts.Patches;

[HarmonyPatch(typeof(MobileParty), nameof(MobileParty.FinishNavigationTransitionInternal))]
internal class PlayerRaftFleetPatches
{
    [HarmonyPrefix]
    private static void Prefix(MobileParty __instance, out bool __state)
    {
        __state = __instance.IsInRaftState;
    }

    [HarmonyPostfix]
    private static void Postfix(MobileParty __instance, bool __state)
    {
        if (ModInformation.IsClient) return;
        if (!__state || __instance.IsInRaftState || !__instance.IsPlayerParty()) return;

        if (!ContainerProvider.TryResolve<IPlayerRaftFleetInterface>(out var raftFleetInterface)) return;

        // Restore fleet on landing on the server instead of vanilla giving it back past the washed ashore menu
        raftFleetInterface.ReturnFleet(__instance);
    }
}
