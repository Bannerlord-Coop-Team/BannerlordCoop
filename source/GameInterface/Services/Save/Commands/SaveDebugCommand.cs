using System;
using Common.Commands;
using Common;
using GameInterface.Services.Heroes.Interfaces;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Text.Json;
using System.Text;
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
            private readonly ISaveInterface saves;

            public SaveSaveAsCoopCommand(ISaveInterface saves)
            {
                this.saves = saves;
            }

            private byte[] ReadFile(string name)
            {
                try { return saves.ReadSaveFile(name); }
                catch (FileNotFoundException) { return null; }
                catch (DirectoryNotFoundException) { return null; }
            }

            public string Prefix => "coop.debug.save";

            public string Name => "save_as";

            public string Description => "Runs the save as debug operation.";

            public CoopCommandSide Side => CoopCommandSide.Server;

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

                if (!saves.CanQueueSave)
                    return Failed("No active campaign / SaveHandler.");
                if (saves.IsSaving)
                    return Failed("A save is already queued.");

                string name = args[0];
                try
                {
                    // A fresh name prevents an old pair from hiding a suppressed or failed write.
                    if (ReadFile(name + ".sav") != null || ReadFile(name + ".json") != null)
                        return Failed("Save name already exists. Choose a fresh name.");

                    var result = saves.SaveCurrentGameToFile(name);
                    if (!result.Success || result.Data == null || result.Data.Length == 0)
                        return Failed("Campaign save was not written.");
                    var campaign = ReadFile(name + ".sav");
                    var sidecar = ReadFile(name + ".json");
                    if (campaign == null || campaign.Length == 0 || sidecar == null || sidecar.Length == 0)
                        return Failed("Save pair is incomplete; both .sav and .json are required.");
                    using (var session = JsonDocument.Parse(Encoding.UTF8.GetString(sidecar).TrimStart('\uFEFF')))
                    {
                        if (session.RootElement.ValueKind != JsonValueKind.Object ||
                            !session.RootElement.TryGetProperty("UniqueGameId", out var id) ||
                            id.ValueKind != JsonValueKind.String || id.GetString() != name ||
                            !session.RootElement.TryGetProperty("Players", out var players) ||
                            players.ValueKind != JsonValueKind.Array)
                            return Failed("Co-op session JSON is incomplete or does not match the save name.");
                    }

                    return Succeeded($"Saved {name}.sav ({campaign.Length} bytes) and {name}.json ({sidecar.Length} bytes).");
                }
                catch (Exception exception)
                {
                    return Failed("Save failed: " + exception.Message);
                }
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

            public CoopCommandSide Side => CoopCommandSide.Server;

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
