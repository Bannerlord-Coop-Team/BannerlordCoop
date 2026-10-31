using Common;
using Coop.Naval.Models.Interfaces;
using GameInterface;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using NavalDLC.GameComponents;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.Library;

namespace Coop.Naval.Models.Patches;

[HarmonyPatch(typeof(NavalDLCBattleRewardModel))]
internal class NavalDLCBattleRewardModelPatches
{
    // Vanilla gives PartyBase.MainParty an extra share of the ships no other winner took, the server gives it to the winning player
    [HarmonyPatch(nameof(NavalDLCBattleRewardModel.DistributeDefeatedPartyShipsAmongWinners))]
    [HarmonyPrefix]
    public static bool DistributeDefeatedPartyShipsAmongWinnersPrefix(NavalDLCBattleRewardModel __instance, ref MBReadOnlyList<KeyValuePair<Ship, MapEventParty>> __result, MBReadOnlyList<Ship> shipsToLoot, MBReadOnlyList<MapEventParty> winnerParties)
    {
        if (ModInformation.IsClient) return true;

        var mainParty = winnerParties
            .Where(x => x.Party.MobileParty?.IsPlayerParty() == true)
            .OrderByDescending(x => x.ContributionToBattle)
            .FirstOrDefault();
        if (mainParty == null) return true;

        if (!ContainerProvider.TryResolve<INavalDLCBattleRewardModelInterface>(out var battleRewardModelInterface)) return true;

        __result = battleRewardModelInterface.DistributeDefeatedPartyShipsAmongWinners(__instance, mainParty, shipsToLoot, winnerParties);

        return false;
    }
}
