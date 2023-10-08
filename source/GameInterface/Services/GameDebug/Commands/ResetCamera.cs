using SandBox.View.Map;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
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

        [CommandLineArgumentFunction("test", "coop.debug")]
        public static string Test(List<string> strings)
        {
            EnterSettlementAction.ApplyForParty(MobileParty.MainParty, Campaign.Current?.Settlements.First());

            return $"Executed {nameof(ResetCameraCommand)}";
        }
    }
}
