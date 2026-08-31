using Common;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Save.Commands
{
    public class SaveDebugCommand
    {
        private static readonly Regex SafeSaveName = new("^[A-Za-z0-9_-]{1,64}$");

#if DEBUG
        private static int evidenceHoldMilliseconds;
#endif

        public static string SaveAs(List<string> args)
        {
            if (!ModInformation.IsServer)
                return "Command can only be run on the server.";
            if (args.Count != 1 || !SafeSaveName.IsMatch(args[0]))
                return "Usage: coop.debug.save.save_as <1-64 letters, digits, underscores, or hyphens>";

            SaveHandler saveHandler = Campaign.Current?.SaveHandler;
            if (saveHandler == null)
                return "No active campaign / SaveHandler.";
            if (saveHandler.IsSaving)
                return "A save is already queued.";

            saveHandler.SaveAs(args[0]);
            return $"Enqueued save as {args[0]} on the server.";
        }

        public static string State(List<string> args)
        {
            if (args.Count != 0)
                return "Usage: coop.debug.save.state";

            SaveHandler saveHandler = Campaign.Current?.SaveHandler;
            return saveHandler == null
                ? "saveHandler=unavailable"
                : $"saveHandler=ready|isSaving={saveHandler.IsSaving}";
        }

        /// <summary>
        /// Enqueues a native autosave (the same path SaveHandler uses on a timer) so the
        /// client save block can be exercised on demand. On a client SetSaveArgs is blocked, so
        /// nothing is enqueued and no file is written; on the host a save file appears.
        /// </summary>
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
