using Common;
using Common.Logging;
using Common.Util;
using GameInterface.Services.Settlements.Interfaces;
using GameInterface.Utils.Commands;
using SandBox.GauntletUI.Map;
using SandBox.View.Map;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Engine;
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

    [CommandLineArgumentFunction("prepare_evidence_map", "coop.debug.ui")]
    public static string PrepareEvidenceMap(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";

        if (args.Count != 0)
            return "Usage: coop.debug.ui.prepare_evidence_map";

        MapScreen mapScreen = MapScreen.Instance;
        if (mapScreen == null)
            return "Campaign map screen is unavailable.";

        try
        {
            // Hide only the client presentation; keep the saved encounter and map event unchanged.
            if (mapScreen.IsInMenu)
            {
                mapScreen._latestMenuContext = null;
                mapScreen.ExitMenuContext();
            }
            mapScreen.RemoveEncounterOverlay();
        }
        catch (Exception ex)
        {
            return CommandHelpers.FormatException("Prepare evidence map", ex);
        }

        return GetEvidenceMapState(mapScreen);
    }

    [CommandLineArgumentFunction("evidence_map_state", "coop.debug.ui")]
    public static string EvidenceMapState(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";

        if (args.Count != 0)
            return "Usage: coop.debug.ui.evidence_map_state";

        MapScreen mapScreen = MapScreen.Instance;
        return mapScreen == null
            ? "Campaign map screen is unavailable."
            : GetEvidenceMapState(mapScreen);
    }

    private static string GetEvidenceMapState(MapScreen mapScreen)
    {
        var cameraView = mapScreen.MapCameraView;
        PartyBase cameraFollowParty = Campaign.Current?.CameraFollowParty;
        string cameraFollowPartyId = cameraFollowParty?.MobileParty?.StringId ?? "null";
        string cameraMode = cameraView?.CurrentCameraFollowMode.ToString() ?? "null";
        bool followTargetReached = false;
        if (cameraView != null && cameraFollowParty != null)
        {
            var followPosition = cameraFollowParty.MapEvent?.Position ?? cameraFollowParty.Position;
            var targetDelta = followPosition.ToVec2() - cameraView._cameraTarget.AsVec2;
            followTargetReached = targetDelta.LengthSquared < 0.0001f;
        }

        return $"menuView={mapScreen.IsInMenu} " +
               $"pendingMenuView={mapScreen._latestMenuContext != null} " +
               $"encounterOverlay={mapScreen._encounterOverlay != null} " +
               $"cameraFollowParty={cameraFollowPartyId} " +
               $"cameraMode={cameraMode} " +
               $"followTargetReached={followTargetReached} " +
               $"animation={cameraView?.CameraAnimationInProgress} " +
               $"fastMove={cameraView?._doFastCameraMovementToTarget} " +
               $"loading={LoadingWindow.IsLoadingWindowActive}";
    }

    [CommandLineArgumentFunction("leave_settlement_encounter", "coop.debug.ui")]
    public static string LeaveSettlementEncounter(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";

        if (args.Count != 0)
            return "Usage: coop.debug.ui.leave_settlement_encounter";

        if (Campaign.Current == null)
            return "Failed: no active campaign.";

        var mainParty = MobileParty.MainParty;
        if (mainParty == null)
            return "Failed: no main party.";

        if (PlayerEncounter.Battle != null || mainParty.MapEvent != null)
            return "Cannot leave the settlement encounter after a battle has started.";

        if (PlayerEncounter.Current == null || PlayerEncounter.EncounterSettlement == null)
            return "No active settlement encounter to leave.";

        if (!ContainerProvider.TryResolve<ISettlementInterface>(out var settlementInterface))
            return "Unable to resolve the settlement interface.";

        try
        {
            using (new AllowedThread())
                settlementInterface.EndSettlementEncounter();
        }
        catch (Exception ex)
        {
            return CommandHelpers.FormatException("Leave settlement encounter", ex);
        }

        return "Cleared the local settlement encounter and returned to the campaign map.";
    }

    [CommandLineArgumentFunction("switch_menu", "coop.debug.ui")]
    public static string SwitchMenu(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";

        if (args.Count != 1)
            return "Usage: coop.debug.ui.switch_menu <menuId>";

        if (Campaign.Current == null)
            return "Failed: no active campaign.";

        try
        {
            GameMenu.SwitchToMenu(args[0]);
        }
        catch (Exception ex)
        {
            return CommandHelpers.FormatException("Switch menu", ex);
        }

        return $"Switched to game menu {args[0]}.";
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

    [CommandLineArgumentFunction("loading_window_state", "coop.debug.ui")]
    public static string LoadingWindowState(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.ui.loading_window_state";

        return $"Loading window: {(LoadingWindow.IsLoadingWindowActive ? "ACTIVE" : "INACTIVE")}.";
    }

    [CommandLineArgumentFunction("saving_overlay_state", "coop.debug.ui")]
    public static string SavingOverlayState(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.ui.saving_overlay_state";

        var dataSource = MapScreen.Instance?
            .GetMapView<GauntletMapSaveView>()?
            ._dataSource;
        if (dataSource == null)
            return "Saving overlay: UNAVAILABLE.";

        return $"Saving overlay: {(dataSource.IsActive ? "ACTIVE" : "INACTIVE")}.";
    }
}
