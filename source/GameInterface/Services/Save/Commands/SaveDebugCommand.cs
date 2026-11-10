using System;
using Common.Commands;
using Common;
using GameInterface.Services.Heroes.Interfaces;
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

        /// <summary>Queues a named save on the server and reports whether the host really took it.</summary>
        public sealed class SaveSaveAsCoopCommand : ICoopCommand
        {
            private readonly ISaveInterface saveInterface;

            public SaveSaveAsCoopCommand(ISaveInterface saveInterface)
            {
                this.saveInterface = saveInterface;
            }

            public string Prefix => "coop.debug.save";

            public string Name => "save_as";

            public string Description => "Runs the save as debug operation.";

            public CoopCommandSide Side => CoopCommandSide.Server;

            public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
            {
                new ExpectedArgs("save_name", "A save name using 1 through 64 letters, digits, underscores, or hyphens.", isRequired: true),
            };

            /// <summary>Validates the name and queues the save, failing when the host didn't take it.</summary>
            public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
            {
                if (!ModInformation.IsServer)
                    return Failed("Command can only be run on the server.");
                if (!SafeSaveName.IsMatch(args[0]))
                    return Failed("Save name must contain 1 through 64 letters, digits, underscores, or hyphens.");
                if (!saveInterface.CanQueueSave)
                    return Failed("No active campaign / SaveHandler.");
                if (saveInterface.IsSaving)
                    return Failed("A save is already queued.");
                if (!saveInterface.TryQueueSave(args[0]))
                    return Failed($"No save as {args[0]} was queued. Check the server log.");

                return Succeeded($"Queued save as {args[0]} on the server.");
            }
        }

        public sealed class SaveStateCoopCommand : ICoopCommand
        {
            public string Prefix => "coop.debug.save";

            public string Name => "state";

            public string Description => "Reports state.";

            public CoopCommandSide Side => CoopCommandSide.Both;

            public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

            public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
            {

                SaveHandler saveHandler = Campaign.Current?.SaveHandler;
                return Succeeded(saveHandler == null
                    ? "saveHandler=unavailable"
                    : $"saveHandler=ready|isSaving={saveHandler.IsSaving}");
            }
        }

        /// <summary>Enqueues a native autosave on the server (the timer's path), failing when nothing was queued.</summary>
        public sealed class SaveForceAutosaveCoopCommand : ICoopCommand
        {
            public string Prefix => "coop.debug.save";

            public string Name => "force_autosave";

            public string Description => "Runs the force autosave debug operation.";

            public CoopCommandSide Side => CoopCommandSide.Server;

            public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
            {
                new ExpectedArgs("evidence_hold_milliseconds", "The optional evidence hold from 1 through 5000 milliseconds.", isRequired: false),
            };

            /// <summary>Validates the evidence hold and queues the autosave, failing when the host didn't take it.</summary>
            public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
            {
                SaveHandler saveHandler = Campaign.Current?.SaveHandler;
                if (saveHandler == null) return Failed("No active campaign / SaveHandler.");

                int holdMilliseconds = 0;
                if (args.Count == 1 &&
                    (int.TryParse(args[0], out holdMilliseconds) == false ||
                     holdMilliseconds < 1 ||
                     holdMilliseconds > 5000))
                {
                    return Failed("Evidence hold must be an integer from 1 through 5000 milliseconds.");
                }

#if !DEBUG
                if (args.Count == 1)
                {
                    return Failed("The evidence hold is only available in DEBUG builds.");
                }
#endif

                if (saveHandler.IsSaving)
                {
                    return Failed("A save is already queued.");
                }

                saveHandler.ForceAutoSave();
                // Disabled autosaves, or a host that blocks the native save queue, leave nothing queued.
                if (!saveHandler.IsSaving)
                {
                    return Failed("No autosave was queued. Autosaves are disabled or this host doesn't use the native save queue.");
                }

#if DEBUG
                if (args.Count == 1)
                {
                    Interlocked.Exchange(ref evidenceHoldMilliseconds, holdMilliseconds);
                }
#endif

                return Succeeded("Enqueued autosave on the server. Check the Saves folder.");
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
