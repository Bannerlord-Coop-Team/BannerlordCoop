#if DEBUG
using Common;
using Common.Commands;
using Newtonsoft.Json;
using System;
using System.Linq;

namespace Coop.Core.Server.Services.Save.Commands;

/// <summary>
/// Arms, reads and releases the session-write observation of one dedicated-server save (DEBUG only).
/// The output carries its own status because the live-test transport does not forward the command result flag.
/// </summary>
public class SessionWriteDebugCommands
{
    private const string Prefix = "coop.debug.save";

    // coop.debug.save.session_arm <attempt_id> <save_name>
    public sealed class SessionArmCoopCommand : ICoopCommand
    {
        private readonly ICoopSessionWriteObserver observer;

        public SessionArmCoopCommand(ICoopSessionWriteObserver observer)
        {
            this.observer = observer;
        }

        public string Prefix => SessionWriteDebugCommands.Prefix;

        public string Name => "session_arm";

        public string Description => "Arms one observation of the co-op session write for the named save.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("attempt_id", "The live-test attempt that owns this observation."),
            new ExpectedArgs("save_name", "The exact save name the next save writes."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args) =>
            Respond(Name, () => observer.Arm(args[0], args[1]));
    }

    // coop.debug.save.session_result <attempt_id>
    public sealed class SessionResultCoopCommand : ICoopCommand
    {
        private readonly ICoopSessionWriteObserver observer;

        public SessionResultCoopCommand(ICoopSessionWriteObserver observer)
        {
            this.observer = observer;
        }

        public string Prefix => SessionWriteDebugCommands.Prefix;

        public string Name => "session_result";

        public string Description => "Reads the armed session-write observation without changing it.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("attempt_id", "The attempt id given to session_arm."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args) =>
            Respond(Name, () => observer.Read(args[0]));
    }

    // coop.debug.save.session_release <attempt_id>
    public sealed class SessionReleaseCoopCommand : ICoopCommand
    {
        private readonly ICoopSessionWriteObserver observer;

        public SessionReleaseCoopCommand(ICoopSessionWriteObserver observer)
        {
            this.observer = observer;
        }

        public string Prefix => SessionWriteDebugCommands.Prefix;

        public string Name => "session_release";

        public string Description => "Releases the armed session-write observation once its current state has been read.";

        public CoopCommandSide Side => CoopCommandSide.Server;

        public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
        {
            new ExpectedArgs("attempt_id", "The attempt id given to session_arm."),
        };

        public CoopCommandResult ProcessCommand(ICoopCommandArgs args) =>
            Respond(Name, () => observer.Release(args[0]));
    }

    internal static CoopCommandResult Respond(string command, Func<SessionWriteObservation> run)
    {
        if (!ModInformation.IsServer)
            return Serialize(command, new SessionWriteObservation("refused", "Command can only be run on the server.", "none", null, null,
                null, Array.Empty<SessionWriteOutcome>(), Array.Empty<string>(), 0, 0, 0, null));

        return Serialize(command, run());
    }

    private static CoopCommandResult Serialize(string command, SessionWriteObservation observation)
    {
        string output = JsonConvert.SerializeObject(new
        {
            proof = "coop-session-written-observer",
            command,
            status = observation.Status,
            reason = observation.Reason,
            result = observation.Result,
            attemptId = observation.AttemptId,
            saveName = observation.SaveName,
            armedUtc = observation.ArmedUtc,
            outcomes = observation.Outcomes.Select(outcome => new { sequence = outcome.Sequence, success = outcome.Success, utc = outcome.Utc }),
            ignoredSaveNames = observation.IgnoredSaveNames,
            ignoredCount = observation.IgnoredCount,
            unarmedCount = observation.UnarmedCount,
            readCount = observation.ReadCount,
            releasedUtc = observation.ReleasedUtc,
        });

        return observation.Status == "refused"
            ? new CoopCommandResult(false, output, "observation_refused")
            : new CoopCommandResult(true, output);
    }
}
#endif
