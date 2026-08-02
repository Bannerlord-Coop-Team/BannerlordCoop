using Common;
using GameInterface.Utils.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Save.Commands
{
    public class SaveDebugCommand
    {
#if DEBUG
        private static int evidenceHoldMilliseconds;

        [CommandLineArgumentFunction("save_as", "coop.debug.save")]
        public static string SaveAs(List<string> args)
        {
            if (!CommandHelpers.IsServerOnlyCommand(out var error, "coop.debug.save.save_as")) return error;
            if (args.Count != 1 ||
                args[0].Length < 1 ||
                args[0].Length > 64 ||
                args[0].Any(character => !char.IsLetterOrDigit(character) && character != '_' && character != '-'))
            {
                return "Usage: coop.debug.save.save_as <1-64 letters, digits, underscores, or hyphens>";
            }

            if (Campaign.Current?.SaveHandler == null) return "No active campaign / SaveHandler.";
            if (Campaign.Current.SaveHandler.IsSaving) return "A save is already queued.";
            if (MBSaveLoad.GetSaveFiles(null).Any(save =>
                string.Equals(save.Name, args[0], StringComparison.OrdinalIgnoreCase)))
            {
                return $"Refusing to overwrite existing save '{args[0]}'.";
            }

            Campaign.Current.SaveHandler.SaveAs(args[0]);
            return $"SAVE_AS_QUEUED name={args[0]}";
        }

        [CommandLineArgumentFunction("status", "coop.debug.save")]
        public static string Status(List<string> args)
        {
            if (args.Count != 0) return "Usage: coop.debug.save.status";
            if (Campaign.Current?.SaveHandler == null) return "SAVE_STATUS campaign=false isSaving=false";

            return $"SAVE_STATUS campaign=true isSaving={Campaign.Current.SaveHandler.IsSaving}";
        }
#endif

        /// <summary>
        /// Enqueues a native autosave (the same path SaveHandler uses on a timer) so the
        /// client save block can be exercised on demand. On a client SetSaveArgs is blocked, so
        /// nothing is enqueued and no file is written; on the host a save file appears.
        /// </summary>
        [CommandLineArgumentFunction("force_autosave", "coop.debug.save")]
        public static string ForceAutoSave(List<string> args)
        {
            if (Campaign.Current?.SaveHandler == null) return "No active campaign / SaveHandler.";

            int holdMilliseconds = 0;
            if (args.Count > 1 ||
                (args.Count == 1 &&
                 (int.TryParse(args[0], out holdMilliseconds) == false ||
                  holdMilliseconds < 1 ||
                  holdMilliseconds > 5000)))
            {
                return "Usage: coop.debug.save.force_autosave [evidence hold milliseconds from 1 to 5000]";
            }

#if DEBUG
            if (args.Count == 1)
            {
                if (ModInformation.IsClient)
                {
                    return "The evidence hold is server-only.";
                }

                if (Campaign.Current.SaveHandler.IsSaving)
                {
                    return "Cannot add an evidence hold while a save is already queued.";
                }
            }
#else
            if (args.Count == 1)
            {
                return "The evidence hold is only available in DEBUG builds.";
            }
#endif

            Campaign.Current.SaveHandler.ForceAutoSave();

#if DEBUG
            if (args.Count == 1)
            {
                if (Campaign.Current.SaveHandler.IsSaving == false)
                {
                    return "Autosaves are disabled; no save was enqueued.";
                }

                Interlocked.Exchange(ref evidenceHoldMilliseconds, holdMilliseconds);
            }
#endif

            string side = ModInformation.IsClient ? "client (save should be BLOCKED)" : "host (save should succeed)";
            return $"Enqueued autosave on {side}. Check the Saves folder.";
        }

        internal static void HoldForEvidenceIfRequested()
        {
#if DEBUG
            int milliseconds = Interlocked.Exchange(ref evidenceHoldMilliseconds, 0);
            if (milliseconds > 0)
            {
                Thread.Sleep(milliseconds);
            }
#endif
        }
    }
}
