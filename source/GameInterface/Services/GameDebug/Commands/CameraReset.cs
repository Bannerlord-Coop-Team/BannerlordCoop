using SandBox.View.Map;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.GameDebug.Commands;

internal class CameraReset
{
    [CommandLineArgumentFunction("fix_camera", "coop.debug")]
    public static string ChangeClanLeader(List<string> strings)
    {
        Game.Current.GameStateManager.UnregisterActiveStateDisableRequest(MapScreen.Instance);
        return "Camera reset";
    }
}
