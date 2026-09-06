using Common.Commands;
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

namespace GameInterface.Services.GameDebug.Commands;

/// <summary>
/// [Debug] UI / screen commands. <c>coop.debug.ui.close_screen</c> forces the current game menu to exit
/// (<see cref="GameMenu.ExitToLast"/>) — a manual escape for when a post-battle encounter screen is left open.
/// </summary>
internal class UiDebugCommands
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    public static readonly ILogger Logger = LogManager.GetLogger<UiDebugCommands>();

    public sealed class UiCloseScreenCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.ui";

        public string Name => "close_screen";

        public string Description => "Runs the close screen debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (Campaign.Current == null)
                return Failed("Failed: no active campaign.");

            try
            {
                GameMenu.ExitToLast();
            }
            catch (Exception ex)
            {
                return Failed(CommandHelpers.FormatException("Close screen", ex));
            }

            return Succeeded("Called GameMenu.ExitToLast().");
        }
    }

    public sealed class UiPrepareEvidenceMapCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.ui";

        public string Name => "prepare_evidence_map";

        public string Description => "Runs the prepare evidence map debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer)
                return Failed("Run this command on a client.");


            MapScreen mapScreen = MapScreen.Instance;
            if (mapScreen == null)
                return Failed("Campaign map screen is unavailable.");

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
                return Failed(CommandHelpers.FormatException("Prepare evidence map", ex));
            }

            return Succeeded(GetEvidenceMapState(mapScreen));
        }
    }

    public sealed class UiEvidenceMapStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.ui";

        public string Name => "evidence_map_state";

        public string Description => "Reports evidence map state.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer)
                return Failed("Run this command on a client.");


            MapScreen mapScreen = MapScreen.Instance;
            if (mapScreen == null)
                return Failed("Campaign map screen is unavailable.");

            return Succeeded(GetEvidenceMapState(mapScreen));
        }
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

    public sealed class UiLeaveSettlementEncounterCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.ui";

        public string Name => "leave_settlement_encounter";

        public string Description => "Runs the leave settlement encounter debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer)
                return Failed("Run this command on a client.");


            if (Campaign.Current == null)
                return Failed("Failed: no active campaign.");

            var mainParty = MobileParty.MainParty;
            if (mainParty == null)
                return Failed("Failed: no main party.");

            if (PlayerEncounter.Battle != null || mainParty.MapEvent != null)
                return Failed("Cannot leave the settlement encounter after a battle has started.");

            if (PlayerEncounter.Current == null || PlayerEncounter.EncounterSettlement == null)
                return Failed("No active settlement encounter to leave.");

            if (!ContainerProvider.TryResolve<ISettlementInterface>(out var settlementInterface))
                return Failed("Unable to resolve the settlement interface.");

            try
            {
                using (new AllowedThread())
                    settlementInterface.EndSettlementEncounter();
            }
            catch (Exception ex)
            {
                return Failed(CommandHelpers.FormatException("Leave settlement encounter", ex));
            }

            return Succeeded("Cleared the local settlement encounter and returned to the campaign map.");
        }
    }

#if DEBUG
    public sealed class UiMapClickOffsetCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.ui";

        public string Name => "map_click_offset";

        public string Description => "Runs the map click offset debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("offset_x", "The horizontal map offset.", isRequired: true),
            new ExpectedArgs("offset_y", "The vertical map offset.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer)
                return Failed("Run this command on a client.");
            if (!float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var offsetX) ||
                !float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var offsetY))
                return Failed("Offsets must be valid numbers.");

            var mapScreen = MapScreen.Instance;
            var mainParty = MobileParty.MainParty;
            if (mapScreen == null || mainParty == null)
                return Failed("Failed: campaign map or main party is unavailable.");
            if (PlayerEncounter.Current != null || mainParty.CurrentSettlement != null)
                return Failed("Leave the active settlement encounter before clicking the campaign map.");
            if (mainParty.MapEvent != null)
                return Failed("Cannot click-to-move while the main party is in a map event.");

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
                return Failed("No nearby navigable map-click target was found.");

            mapScreen.HandleLeftMouseButtonClick(null, target, target.Face, false);

            return Succeeded($"Issued a real campaign-map click from {current.X:R},{current.Y:R} " +
                $"to {target.X:R},{target.Y:R}; time={Campaign.Current.TimeControlMode}; " +
                $"behavior={mainParty.DefaultBehavior}; target={mainParty.TargetPosition.X:R},{mainParty.TargetPosition.Y:R}.");
        }
    }

    public sealed class UiMapMovementStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.ui";

        public string Name => "map_movement_state";

        public string Description => "Reports map movement state.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer)
                return Failed("Run this command on a client.");

            var mainParty = MobileParty.MainParty;
            if (mainParty == null || Campaign.Current == null)
                return Failed("Failed: no active campaign or main party.");

            return Succeeded($"position={mainParty.Position.X:R},{mainParty.Position.Y:R}|" +
                $"target={mainParty.TargetPosition.X:R},{mainParty.TargetPosition.Y:R}|" +
                $"behavior={mainParty.DefaultBehavior}|" +
                $"settlement={mainParty.CurrentSettlement?.StringId ?? "none"}|" +
                $"encounter={PlayerEncounter.EncounterSettlement?.StringId ?? "none"}|" +
                $"time={Campaign.Current.TimeControlMode}");
        }
    }
#endif

    public sealed class UiSwitchMenuCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.ui";

        public string Name => "switch_menu";

        public string Description => "Runs the switch menu debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("menu_id", "The game menu id.", isRequired: true),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {
            if (ModInformation.IsServer)
                return Failed("Run this command on a client.");


            if (Campaign.Current == null)
                return Failed("Failed: no active campaign.");

            try
            {
                GameMenu.SwitchToMenu(args[0]);
            }
            catch (Exception ex)
            {
                return Failed(CommandHelpers.FormatException("Switch menu", ex));
            }

            return Succeeded($"Switched to game menu {args[0]}.");
        }
    }

    public sealed class UiPopStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.ui";

        public string Name => "pop_state";

        public string Description => "Reports pop state.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            TaleWorlds.Core.GameState activeState = Game.Current?.GameStateManager?.ActiveState;
            if (activeState == null)
                return Failed("Failed: no active game state.");

            if (activeState is MapState)
                return Failed("Active state is already MapState.");

            Game.Current.GameStateManager.PopState();
            return Succeeded($"Queued pop for {activeState.GetType().Name}.");
        }
    }

    public sealed class UiActiveStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.ui";

        public string Name => "active_state";

        public string Description => "Reports active state.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            return Succeeded(Game.Current?.GameStateManager?.ActiveState?.GetType().Name ?? "none");
        }
    }

    public sealed class UiLoadingWindowStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.ui";

        public string Name => "loading_window_state";

        public string Description => "Reports loading window state.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            return Succeeded($"Loading window: {(LoadingWindow.IsLoadingWindowActive ? "ACTIVE" : "INACTIVE")}.");
        }
    }

    public sealed class UiSavingOverlayStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.ui";

        public string Name => "saving_overlay_state";

        public string Description => "Reports saving overlay state.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
        {

            var dataSource = MapScreen.Instance?
                .GetMapView<GauntletMapSaveView>()?
                ._dataSource;
            if (dataSource == null)
                return Failed("Saving overlay: UNAVAILABLE.");

            return Succeeded($"Saving overlay: {(dataSource.IsActive ? "ACTIVE" : "INACTIVE")}.");
        }
    }
}
