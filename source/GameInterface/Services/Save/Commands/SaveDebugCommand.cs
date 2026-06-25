using Common;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Save.Commands
{
    public class SaveDebugCommand
    {
        /// <summary>
        /// Enqueues a native autosave (the same path SaveHandler uses on a timer) so the
        /// client save block can be exercised on demand. On a client SetSaveArgs is blocked, so
        /// nothing is enqueued and no file is written; on the host a save file appears.
        /// </summary>
        [CommandLineArgumentFunction("force_autosave", "coop.debug.save")]
        public static string ForceAutoSave(List<string> args)
        {
            if (Campaign.Current?.SaveHandler == null) return "No active campaign / SaveHandler.";

            Campaign.Current.SaveHandler.ForceAutoSave();

            string side = ModInformation.IsClient ? "client (save should be BLOCKED)" : "host (save should succeed)";
            return $"Enqueued autosave on {side}. Check the Saves folder.";
        }
    }
}
