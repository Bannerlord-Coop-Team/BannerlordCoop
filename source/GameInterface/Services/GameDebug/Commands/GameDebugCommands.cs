using System.Collections.Generic;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.GameDebug.Commands
{
    public class GameDebugCommands
    {
        public static bool ForceSync = false;

        [CommandLineArgumentFunction("force_sync", "Coop.Debug.Client")]
        public static string forcesync(List<string> args)
        {
            if (ModInformation.IsServer)
            {
                return "Only available on client";
            }

            ForceSync = true;
            return "Forced";
        }
    }
}
