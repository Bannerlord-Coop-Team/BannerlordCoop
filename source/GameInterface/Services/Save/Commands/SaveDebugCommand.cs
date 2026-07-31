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
        /// save block can be exercised on demand. SetSaveArgs is blocked on a client and on a
        /// headless host — see <see cref="Patches.SaveHandlerAutoSaveBlockPatch"/> — so nothing is
        /// enqueued and no file is written there; on a graphical host a save file appears.
        /// </summary>
        [CommandLineArgumentFunction("force_autosave", "coop.debug.save")]
        public static string ForceAutoSave(List<string> args)
        {
            if (Campaign.Current?.SaveHandler == null) return "No active campaign / SaveHandler.";

            Campaign.Current.SaveHandler.ForceAutoSave();

            string side;
            if (ModInformation.IsClient) side = "client (save should be BLOCKED)";
            else if (ModInformation.IsHeadlessHost) side = "headless host (save should be BLOCKED — the host owns the save schedule)";
            else side = "graphical host (save should succeed)";

            return $"Enqueued autosave on {side}. Check the Saves folder.";
        }
    }
}
