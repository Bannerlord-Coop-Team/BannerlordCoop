using Common.Messaging;
using Coop.Naval.PortScreen.Data;
using Coop.Naval.PortScreen.Messages;
using HarmonyLib;
using NavalDLC.ViewModelCollection.Port.PortScreenHandlers;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace Coop.Naval.PortScreen.Patches;

[HarmonyPatch]
internal class PortScreenHandlerPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.DeclaredMethod(typeof(PortScreenTradeModeHandler), nameof(PortScreenTradeModeHandler.OnConfirmChanges));
        yield return AccessTools.DeclaredMethod(typeof(PortScreenLootModeHandler), nameof(PortScreenLootModeHandler.OnConfirmChanges));
        yield return AccessTools.DeclaredMethod(typeof(PortScreenManageFleetModeHandler), nameof(PortScreenManageFleetModeHandler.OnConfirmChanges));
        yield return AccessTools.DeclaredMethod(typeof(PortScreenManageOtherFleetModeHandler), nameof(PortScreenManageOtherFleetModeHandler.OnConfirmChanges));
    }

    [HarmonyPrefix]
    public static bool OnConfirmChangesPrefix(PortScreenHandler __instance)
    {
        // Map the child handler type to a PortScreenMode and the sides it uses when confirming changes
        if (!TryGetModeAndSides(__instance, out var mode, out var rightSide, out var leftSide)) return false;

        // All OnConfirmChanges calls use the same messaging path
        var message = new PortScreenChangesRequested(
            mode,
            rightSide,
            leftSide,
            Hero.MainHero,
            Clan.PlayerClan,
            MobileParty.MainParty,
            new MBReadOnlyList<Ship>(__instance.ShipsToBuy.Select(info => info.Ship)),
            new MBReadOnlyList<Ship>(__instance.ShipsToSell.Select(info => info.Ship)),
            __instance.ShipsToRepair,
            __instance.ShipsToRename,
            __instance.SelectedShipPieces,
            __instance.SelectedFigureheads,
            __instance.ShipsToSend);
        MessageBroker.Instance.Publish(__instance, message);

        return false;
    }

    private static bool TryGetModeAndSides(
        PortScreenHandler handler,
        out PortScreenMode mode,
        out PartyBase rightSide,
        out PartyBase leftSide)
    {
        rightSide = null;
        leftSide = null;

        switch (handler)
        {
            case PortScreenTradeModeHandler trade:
                mode = PortScreenMode.Trade;
                rightSide = trade._rightOwner;
                leftSide = trade._leftOwner;
                return true;
            case PortScreenLootModeHandler loot:
                mode = PortScreenMode.Loot;
                return true;
            case PortScreenManageFleetModeHandler manageFleet:
                mode = PortScreenMode.ManageFleet;
                rightSide = manageFleet._rightSide;
                return true;
            case PortScreenManageOtherFleetModeHandler manageOtherFleet:
                mode = PortScreenMode.ManageOtherFleet;
                rightSide = manageOtherFleet._other;
                return true;
            default:
                mode = default;
                return false;
        }
    }
}
