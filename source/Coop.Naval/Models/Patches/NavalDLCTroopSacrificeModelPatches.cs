using Common;
using Coop.Naval.Models.Interfaces;
using GameInterface;
using HarmonyLib;
using NavalDLC.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace Coop.Naval.Models.Patches;

[HarmonyPatch(typeof(NavalDLCTroopSacrificeModel))]
internal class NavalDLCTroopSacrificeModelPatches
{
    // The server applies a player's leave soldiers behind, which has no PlayerEncounter or MobileParty.MainParty
    [HarmonyPatch(nameof(NavalDLCTroopSacrificeModel.GetShipsToSacrificeForTryingToGetAway))]
    [HarmonyPrefix]
    public static bool GetShipsToSacrificeForTryingToGetAwayPrefix(BattleSideEnum playerBattleSide, MapEvent mapEvent, out MBList<Ship> shipsToCapture, out Ship shipToTakeDamage, out float damageToApplyForLastShip)
    {
        shipsToCapture = new MBList<Ship>();
        shipToTakeDamage = null;
        damageToApplyForLastShip = float.MinValue;

        if (ModInformation.IsClient) return true;

        if (!ContainerProvider.TryResolve<INavalDLCTroopSacrificeModelInterface>(out var troopSacrificeModelInterface)) return false;

        // Only the defender side's leader can try to get away, which is the fleeing player's party
        var mainParty = mapEvent.GetLeaderParty(playerBattleSide).MobileParty;
        troopSacrificeModelInterface.GetShipsToSacrificeForTryingToGetAway(mainParty, playerBattleSide, mapEvent, out shipsToCapture, out shipToTakeDamage, out damageToApplyForLastShip);

        return false;
    }
}
