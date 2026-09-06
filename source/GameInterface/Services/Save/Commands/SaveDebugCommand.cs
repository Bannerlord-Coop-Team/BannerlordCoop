using System;
using Common.Commands;
using Common;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using TaleWorlds.CampaignSystem;
using static TaleWorlds.Library.CommandLineFunctionality;

namespace GameInterface.Services.Save.Commands
{
    public class SaveDebugCommand
    {
        private static CoopCommandResult Succeeded(string output) =>
            new CoopCommandResult(true, output);

        private static CoopCommandResult Failed(string output) =>
            new CoopCommandResult(false, output, "command_failed");

        private static readonly Regex SafeSaveName = new("^[A-Za-z0-9_-]{1,64}$");

#if DEBUG
        private static int evidenceHoldMilliseconds;
#endif

        public sealed class SaveSaveAsCoopCommand : ICoopCommand
        {
            public string Prefix => "coop.debug.save";

            public string Name => "save_as";

            public string Description => "Runs the save as debug operation.";

            public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
            {
                new ExpectedArgs("save_name", "A save name using 1 through 64 letters, digits, underscores, or hyphens.", isRequired: true),
            };

            public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
            {
                if (!ModInformation.IsServer)
                    return Failed("Command can only be run on the server.");
                if (!SafeSaveName.IsMatch(args[0]))
                    return Failed("Save name must contain 1 through 64 letters, digits, underscores, or hyphens.");

                SaveHandler saveHandler = Campaign.Current?.SaveHandler;
                if (saveHandler == null)
                    return Failed("No active campaign / SaveHandler.");
                if (saveHandler.IsSaving)
                    return Failed("A save is already queued.");

                saveHandler.SaveAs(args[0]);
                return Succeeded($"Enqueued save as {args[0]} on the server.");
            }
        }

        public sealed class SaveStateCoopCommand : ICoopCommand
        {
            public string Prefix => "coop.debug.save";

            public string Name => "state";

            public string Description => "Reports state.";

            public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

            public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
            {

                SaveHandler saveHandler = Campaign.Current?.SaveHandler;
                return Succeeded(saveHandler == null
                    ? "saveHandler=unavailable"
                    : $"saveHandler=ready|isSaving={saveHandler.IsSaving}");
            }
        }

        /// <summary>
        /// Enqueues a native autosave (the same path SaveHandler uses on a timer) so the
        /// client save block can be exercised on demand. On a client SetSaveArgs is blocked, so
        /// nothing is enqueued and no file is written; on the host a save file appears.
        /// </summary>
        public sealed class SaveForceAutosaveCoopCommand : ICoopCommand
        {
            public string Prefix => "coop.debug.save";

            public string Name => "force_autosave";

            public string Description => "Runs the force autosave debug operation.";

            public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
            {
                new ExpectedArgs("evidence_hold_milliseconds", "The optional evidence hold from 1 through 5000 milliseconds.", isRequired: false),
            };

            public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
            {
                            if (Campaign.Current?.SaveHandler == null) return Failed("No active campaign / SaveHandler.");

                            int holdMilliseconds = 0;
                            if (args.Count == 1 &&
                                (int.TryParse(args[0], out holdMilliseconds) == false ||
                                 holdMilliseconds < 1 ||
                                 holdMilliseconds > 5000))
                            {
                                return Failed("Evidence hold must be an integer from 1 through 5000 milliseconds.");
                            }

                #if DEBUG
                            if (args.Count == 1)
                            {
                                if (ModInformation.IsClient)
                                {
                                    return Failed("The evidence hold is server-only.");
                                }

                                if (Campaign.Current.SaveHandler.IsSaving)
                                {
                                    return Failed("Cannot add an evidence hold while a save is already queued.");
                                }
                            }
                #else
                            if (args.Count == 1)
                            {
                                return Failed("The evidence hold is only available in DEBUG builds.");
                            }
                #endif

                            Campaign.Current.SaveHandler.ForceAutoSave();

                #if DEBUG
                            if (args.Count == 1)
                            {
                                if (Campaign.Current.SaveHandler.IsSaving == false)
                                {
                                    return Failed("Autosaves are disabled; no save was enqueued.");
                                }

                                Interlocked.Exchange(ref evidenceHoldMilliseconds, holdMilliseconds);
                            }
                #endif

                            string side = ModInformation.IsClient ? "client (save should be BLOCKED)" : "host (save should succeed)";
                            return Succeeded($"Enqueued autosave on {side}. Check the Saves folder.");
            }
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
