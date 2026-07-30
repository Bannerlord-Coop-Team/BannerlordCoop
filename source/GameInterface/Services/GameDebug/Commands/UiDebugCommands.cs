using Common;
using Common.Logging;
using Common.Util;
using GameInterface.Utils.Commands;
using Helpers;
using SandBox.GauntletUI;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.ScreenSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.GameDebug.Commands;

/// <summary>
/// [Debug] UI / screen commands. <c>coop.debug.ui.close_screen</c> forces the current game menu to exit
/// (<see cref="GameMenu.ExitToLast"/>) — a manual escape for when a post-battle encounter screen is left open.
/// </summary>
internal class UiDebugCommands
{
    public static readonly ILogger Logger = LogManager.GetLogger<UiDebugCommands>();

    private const string CloseScreenUsage =
@"Usage:
  coop.debug.ui.close_screen

Exits the current game menu (GameMenu.ExitToLast). Use to dismiss a post-battle encounter screen left open.";

    [CommandLineArgumentFunction("close_screen", "coop.debug.ui")]
    public static string CloseScreen(List<string> args)
    {
        var ctx = new CommandContext("close_screen", CloseScreenUsage, args);
        if (!ctx.RequireArgCount(0, out var error))
            return error;

        if (Campaign.Current == null)
            return "Failed: no active campaign.";

        try
        {
            GameMenu.ExitToLast();
        }
        catch (Exception ex)
        {
            return CommandHelpers.FormatException("Close screen", ex);
        }

        return "Called GameMenu.ExitToLast().";
    }

    [CommandLineArgumentFunction("pop_state", "coop.debug.ui")]
    public static string PopState(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.ui.pop_state";

        TaleWorlds.Core.GameState activeState = Game.Current?.GameStateManager?.ActiveState;
        if (activeState == null)
            return "Failed: no active game state.";

        if (activeState is MapState)
            return "Active state is already MapState.";

        Game.Current.GameStateManager.PopState();
        return $"Queued pop for {activeState.GetType().Name}.";
    }

    [CommandLineArgumentFunction("active_state", "coop.debug.ui")]
    public static string ActiveState(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.ui.active_state";

        return Game.Current?.GameStateManager?.ActiveState?.GetType().Name ?? "none";
    }

    [CommandLineArgumentFunction("complete_loot_party_screen", "coop.debug.ui")]
    public static string CompleteLootPartyScreen(List<string> args)
    {
        if (!ModInformation.IsClient)
            return "Command can only be run on a client.";
        if (args.Count != 0)
            return "Usage: coop.debug.ui.complete_loot_party_screen";

        if (Game.Current?.GameStateManager?.ActiveState is not PartyState partyState ||
            partyState.PartyScreenLogic == null)
            return "Failed: active state is not a PartyState.";
        if (partyState.PartyScreenMode != PartyScreenHelper.PartyScreenMode.Loot)
            return $"Failed: active Party screen mode is {partyState.PartyScreenMode}, not Loot.";

        var logic = partyState.PartyScreenLogic;
        var rightMemberRoster =
            logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Right];
        var rightPrisonerRoster =
            logic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Right];
        var membersBefore = rightMemberRoster.TotalManCount;
        var prisonersBefore = rightPrisonerRoster.TotalManCount;

        using (new AllowedThread())
        {
            var members = new PartyScreenLogic.PartyCommand();
            members.FillForTransferAllTroops(
                PartyScreenLogic.PartyRosterSide.Left,
                PartyScreenLogic.TroopType.Member);
            logic.AddCommand(members);

            var prisoners = new PartyScreenLogic.PartyCommand();
            prisoners.FillForTransferAllTroops(
                PartyScreenLogic.PartyRosterSide.Left,
                PartyScreenLogic.TroopType.Prisoner);
            logic.AddCommand(prisoners);
            logic.RemoveZeroCounts();
        }

        var acceptedMembers = rightMemberRoster.TotalManCount - membersBefore;
        var acceptedPrisoners = rightPrisonerRoster.TotalManCount - prisonersBefore;
        PartyScreenHelper.CloseScreen(isForced: false);
        if (Game.Current.GameStateManager.ActiveState == partyState)
            return "Failed: Party screen Done did not close the Loot state.";

        return $"PARTY_LOOT_COMPLETED acceptedMembers={acceptedMembers}, " +
               $"acceptedPrisoners={acceptedPrisoners}, mode=Loot.";
    }

    [CommandLineArgumentFunction("complete_loot_inventory_screen", "coop.debug.ui")]
    public static string CompleteLootInventoryScreen(List<string> args)
    {
        if (!ModInformation.IsClient)
            return "Command can only be run on a client.";
        if (args.Count != 0)
            return "Usage: coop.debug.ui.complete_loot_inventory_screen";

        if (Game.Current?.GameStateManager?.ActiveState is not InventoryState inventoryState ||
            inventoryState.InventoryLogic == null)
            return "Failed: active state is not an InventoryState.";
        if (inventoryState.InventoryMode != InventoryScreenHelper.InventoryMode.Loot)
            return $"Failed: active Inventory screen mode is {inventoryState.InventoryMode}, not Loot.";
        if (inventoryState.Handler is not GauntletInventoryScreen inventoryScreen ||
            ScreenManager.TopScreen != inventoryScreen)
            return "Failed: the Loot Inventory screen is not initialized on top.";

        var itemRoster = PartyBase.MainParty?.ItemRoster;
        if (itemRoster == null)
            return "Failed: local player party has no item roster.";

        var itemsBefore = itemRoster.Sum(element => element.Amount);
        inventoryScreen.ExecuteTakeAll();
        var acceptedItems = itemRoster.Sum(element => element.Amount) - itemsBefore;

        InventoryScreenHelper.CloseScreen(fromCancel: false);
        if (Game.Current.GameStateManager.ActiveState == inventoryState)
            return "Failed: Inventory screen Done did not close the Loot state.";

        return $"INVENTORY_LOOT_COMPLETED acceptedItems={acceptedItems}, mode=Loot.";
    }
}
