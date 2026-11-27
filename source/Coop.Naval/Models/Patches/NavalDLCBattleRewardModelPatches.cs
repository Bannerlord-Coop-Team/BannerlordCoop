using Common;
using Coop.Naval.Models.Interfaces;
using GameInterface;
using GameInterface.Services.PartyBases.Extensions;
using HarmonyLib;
using NavalDLC.GameComponents;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.Library;

namespace Coop.Naval.Models.Patches;

[HarmonyPatch(typeof(NavalDLCBattleRewardModel))]
[HarmonyPatchCategory(GameInterface.GameInterface.HARMONY_NAVAL_CATEGORY)]
internal class NavalDLCBattleRewardModelPatches
{
    // Vanilla gives PartyBase.MainParty an extra share of the ships no other winner took, the server splits it among the winning players
    [HarmonyPatch(nameof(NavalDLCBattleRewardModel.DistributeDefeatedPartyShipsAmongWinners))]
    [HarmonyPrefix]
    public static bool DistributeDefeatedPartyShipsAmongWinnersPrefix(NavalDLCBattleRewardModel __instance, ref MBReadOnlyList<KeyValuePair<Ship, MapEventParty>> __result, MBReadOnlyList<Ship> shipsToLoot, MBReadOnlyList<MapEventParty> winnerParties)
    {
        if (ModInformation.IsClient) return true;

        if (!winnerParties.Any(x => x.Party.IsPlayerParty())) return true;

        if (!ContainerProvider.TryResolve<INavalDLCBattleRewardModelInterface>(out var battleRewardModelInterface)) return true;

        __result = battleRewardModelInterface.DistributeDefeatedPartyShipsAmongWinners(__instance, shipsToLoot, winnerParties);

        return false;
    }
}
