using System;
using Common.Commands;
using SandBox.View.Map;
using System.Collections.Generic;
using TaleWorlds.Core;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.GameDebug.Commands;

internal class CameraReset
{
    private static CoopCommandResult Succeeded(string output) =>
        new CoopCommandResult(true, output);

    private static CoopCommandResult Failed(string output) =>
        new CoopCommandResult(false, output, "command_failed");

    public sealed class FixCameraCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug";

        public string Name => "fix_camera";

        public string Description => "Runs the fix camera debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {
            Game.Current.GameStateManager.UnregisterActiveStateDisableRequest(MapScreen.Instance);
            return Succeeded("Camera reset");
        }
    }

    public sealed class MapCameraFocusMainPartyCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_camera";

        public string Name => "focus_main_party";

        public string Description => "Runs the focus main party debug operation.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {

            MapCameraView cameraView = MapScreen.Instance?.MapCameraView;
            if (cameraView == null)
            {
                return Failed("Campaign map camera is unavailable");
            }

            cameraView.TeleportCameraToMainParty();
            return Succeeded(GetCameraState(cameraView));
        }
    }

    public sealed class MapCameraStateCoopCommand : ICoopCommand
    {
        public string Prefix => "coop.debug.map_camera";

        public string Name => "state";

        public string Description => "Reports state.";

        public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

        public CoopCommandResult ProcessCommand(ICoopCommandArgs strings)
        {

            MapCameraView cameraView = MapScreen.Instance?.MapCameraView;
            if (cameraView == null)
                return Failed("Campaign map camera is unavailable");

            return Succeeded(GetCameraState(cameraView));
        }
    }

    private static string GetCameraState(MapCameraView cameraView)
    {
        return $"locked={cameraView.IsCameraLockedToPlayerParty()} " +
               $"animation={cameraView.CameraAnimationInProgress} " +
               $"fastMove={cameraView._doFastCameraMovementToTarget}";
    }
}
