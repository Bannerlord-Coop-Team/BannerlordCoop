using Common;
using Coop.Naval.Models.Interfaces;
using GameInterface;
using HarmonyLib;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Coop.Naval.MapEvents.Patches;

[HarmonyPatch(typeof(MapEvent))]
internal class MapEventPatches
{
    // Vanilla only rolls a figurehead for the main party, the server rolls one for each winning player
    [HarmonyPatch(nameof(MapEvent.CalculatePlayerFigureheadShare))]
    [HarmonyPostfix]
    public static void CalculatePlayerFigureheadSharePostfix(MapEvent __instance, MBList<MapEventParty> defeatedParties, PartyBase defeatedLeaderParty)
    {
        if (ModInformation.IsClient) return;
        if (!__instance.IsNavalMapEvent || __instance.WinningSide == BattleSideEnum.None) return;

        if (!ContainerProvider.TryResolve<INavalDLCBattleRewardModelInterface>(out var battleRewardModelInterface)) return;

        battleRewardModelInterface.CalculatePlayerFigureheadShare(__instance, defeatedParties, defeatedLeaderParty);
    }
}
