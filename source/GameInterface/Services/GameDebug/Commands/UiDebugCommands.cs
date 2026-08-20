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
using System.Globalization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using static TaleWorlds.Library.CommandLineFunctionality;
#if DEBUG
using GameInterface.Services.UI.CoopOptions;
using System.Text.Json;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.ScreenSystem;
#endif

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

#if DEBUG
    [CommandLineArgumentFunction("open_player_nameplates_options", "coop.debug.ui")]
    public static string OpenPlayerNameplatesOptions(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";
        if (args.Count != 0)
            return "Usage: coop.debug.ui.open_player_nameplates_options";
        if (LoadingWindow.IsLoadingWindowActive)
            return "Wait for the loading window to close before opening co-op options.";
        if (MapScreen.Instance == null)
            return "The campaign map screen is unavailable.";

        if (ScreenManager.TopScreen is CoopOptionsUI)
            return "Co-op options screen is already open.";

        try
        {
            var optionsScreen = ViewCreatorManager.CreateScreenView<CoopOptionsUI>() as CoopOptionsUI;
            if (optionsScreen == null)
                return "Unable to create the player nameplates options screen.";
            optionsScreen.SelectPlayerNameplatesTabForDebug();
            ScreenManager.PushScreen(optionsScreen);
        }
        catch (Exception ex)
        {
            return CommandHelpers.FormatException("Open player nameplates options", ex);
        }

        return GetPlayerNameplatesOptionsScreenState();
    }

    [CommandLineArgumentFunction("player_nameplates_options_screen_state", "coop.debug.ui")]
    public static string PlayerNameplatesOptionsScreenState(List<string> args)
    {
        if (args.Count != 0)
            return "Usage: coop.debug.ui.player_nameplates_options_screen_state";

        return GetPlayerNameplatesOptionsScreenState();
    }

    [CommandLineArgumentFunction("close_player_nameplates_options", "coop.debug.ui")]
    public static string ClosePlayerNameplatesOptions(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";
        if (args.Count != 0)
            return "Usage: coop.debug.ui.close_player_nameplates_options";
        if (!(ScreenManager.TopScreen is CoopOptionsUI))
            return "Player nameplates options screen is not open.";

        ScreenManager.PopScreen();
        return GetPlayerNameplatesOptionsScreenState();
    }

    private static string GetPlayerNameplatesOptionsScreenState()
    {
        var optionsScreen = ScreenManager.TopScreen as CoopOptionsUI;
        return "LIVE_TEST_JSON=" + JsonSerializer.Serialize(new
        {
            active = optionsScreen != null,
            playerNameplatesTabSelected = optionsScreen?.IsPlayerNameplatesTabSelectedForDebug == true
        });
    }

    [CommandLineArgumentFunction("map_click_offset", "coop.debug.ui")]
    public static string MapClickOffset(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";
        if (args.Count != 2 ||
            !float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var offsetX) ||
            !float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var offsetY))
            return "Usage: coop.debug.ui.map_click_offset <offsetX> <offsetY>";

        var mapScreen = MapScreen.Instance;
        var mainParty = MobileParty.MainParty;
        if (mapScreen == null || mainParty == null)
            return "Failed: campaign map or main party is unavailable.";
        if (PlayerEncounter.Current != null || mainParty.CurrentSettlement != null)
            return "Leave the active settlement encounter before clicking the campaign map.";
        if (mainParty.MapEvent != null)
            return "Cannot click-to-move while the main party is in a map event.";

        var current = mainParty.Position;
        var offsets = new[]
        {
            new Vec2(offsetX, offsetY),
            new Vec2(-offsetY, offsetX),
            new Vec2(-offsetX, -offsetY),
            new Vec2(offsetY, -offsetX),
        };
        CampaignVec2 target = default;
        bool targetFound = false;
        foreach (var offset in offsets)
        {
            var candidate = new CampaignVec2(
                new Vec2(current.X + offset.x, current.Y + offset.y),
                current.IsOnLand);
            if (!candidate.Face.IsValid() ||
                !mapScreen.MapScene.DoesPathExistBetweenFaces(
                    candidate.Face.FaceIndex,
                    mainParty.CurrentNavigationFace.FaceIndex,
                    false))
                continue;

            target = candidate;
            targetFound = true;
            break;
        }
        if (!targetFound)
            return "No nearby navigable map-click target was found.";

        mapScreen.HandleLeftMouseButtonClick(null, target, target.Face, false);

        return
            $"Issued a real campaign-map click from {current.X:R},{current.Y:R} " +
            $"to {target.X:R},{target.Y:R}; time={Campaign.Current.TimeControlMode}; " +
            $"behavior={mainParty.DefaultBehavior}; target={mainParty.TargetPosition.X:R},{mainParty.TargetPosition.Y:R}.";
    }

    [CommandLineArgumentFunction("map_movement_state", "coop.debug.ui")]
    public static string MapMovementState(List<string> args)
    {
        if (ModInformation.IsServer)
            return "Run this command on a client.";
        if (args.Count != 0)
            return "Usage: coop.debug.ui.map_movement_state";

        var mainParty = MobileParty.MainParty;
        if (mainParty == null || Campaign.Current == null)
            return "Failed: no active campaign or main party.";

        return
            $"position={mainParty.Position.X:R},{mainParty.Position.Y:R}|" +
            $"target={mainParty.TargetPosition.X:R},{mainParty.TargetPosition.Y:R}|" +
            $"behavior={mainParty.DefaultBehavior}|" +
            $"settlement={mainParty.CurrentSettlement?.StringId ?? "none"}|" +
            $"encounter={PlayerEncounter.EncounterSettlement?.StringId ?? "none"}|" +
            $"time={Campaign.Current.TimeControlMode}";
    }
#endif

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
