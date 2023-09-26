using SandBox.View.Map;
using System.Collections.Generic;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.GameDebug.Commands
{
    internal class ResetCamera
    {
        [CommandLineArgumentFunction("reset_camera", "coop.debug")]
        public static string ResetCameraCommand(List<string> strings)
        {
            var cameraView = MapScreen.Instance?.GetMapView<MapCameraView>();

            if (cameraView == null) return "Unable to find camera";

            cameraView.ResetCamera(true, true);

            return "Camera reset";
        }
    }
}
