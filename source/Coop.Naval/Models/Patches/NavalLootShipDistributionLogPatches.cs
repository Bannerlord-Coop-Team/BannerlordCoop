using Common;
using Common.Logging;
using GameInterface;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using NavalDLC.GameComponents;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.Library;

namespace Coop.Naval.Models.Patches;

/// <summary>[Server] Logs each defeated party's ship the loot distribution gives to no winner, which the battle results destroy.</summary>
[HarmonyPatch(typeof(NavalDLCBattleRewardModel))]
internal class NavalLootShipDistributionLogPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalLootShipDistributionLogPatches>();

    // A postfix runs after the co-op distribution prefix too, so it reads the list LootDefeatedPartyShips destroys the unassigned ships from.
    [HarmonyPatch(nameof(NavalDLCBattleRewardModel.DistributeDefeatedPartyShipsAmongWinners))]
    [HarmonyPostfix]
    private static void DistributeDefeatedPartyShipsAmongWinnersPostfix(MBReadOnlyList<KeyValuePair<Ship, MapEventParty>> __result)
    {
        if (ModInformation.IsClient || __result == null) return;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager)) return;

        foreach (var distributed in __result)
        {
            if (distributed.Value != null) continue;

            objectManager.TryGetId(distributed.Key, out var shipId);
            Logger.Information("[NavalSync] Loot ship {ShipId} of {Loser} went to no winner; the battle results destroy it",
                shipId, distributed.Key.Owner?.Id);
        }
    }
}
