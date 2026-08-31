using SandBox.View.Map;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace GameInterface.Services.GameDebug.Commands;

internal class CameraReset
{
    public static string ChangeClanLeader(List<string> strings)
    {
        Game.Current.GameStateManager.UnregisterActiveStateDisableRequest(MapScreen.Instance);
        return "Camera reset";
    }

    public static string FocusMainParty(List<string> strings)
    {
        if (strings.Count != 0)
        {
            return "Usage: coop.debug.map_camera.focus_main_party";
        }

        MapCameraView cameraView = MapScreen.Instance?.MapCameraView;
        if (cameraView == null)
        {
            return "Campaign map camera is unavailable";
        }

        cameraView.TeleportCameraToMainParty();
        return GetCameraState(cameraView);
    }

    public static string GetState(List<string> strings)
    {
        if (strings.Count != 0)
        {
            return "Usage: coop.debug.map_camera.state";
        }

        MapCameraView cameraView = MapScreen.Instance?.MapCameraView;
        return cameraView == null
            ? "Campaign map camera is unavailable"
            : GetCameraState(cameraView);
    }

    private static string GetCameraState(MapCameraView cameraView)
    {
        return $"locked={cameraView.IsCameraLockedToPlayerParty()} " +
               $"animation={cameraView.CameraAnimationInProgress} " +
               $"fastMove={cameraView._doFastCameraMovementToTarget}";
    }
}
