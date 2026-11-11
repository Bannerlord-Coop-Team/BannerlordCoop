#if DEBUG
using Common.Commands;
using Missions.Battles;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>Withholds this client's completion announcements to exercise the kept-host deadline with transport alive.</summary>
public sealed class NavalResultSilenceCoopCommand : ICoopCommand
{
    public string Prefix => "coop.debug.naval";
    public string Name => "result_silence";
    public string Description => "Withholds this client's battle result and departure announcements; on, off, or no argument for status.";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs { get; } = new IExpectedArgs[]
    {
        new ExpectedArgs("state", "on or off; omitted reports the current mission's state", false),
    };

    // The client owns its outgoing reports; this fixture deliberately leaves server and mesh transport running.
    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        if (args.Count > 1 || (args.Count == 1 && args[0] != "on" && args[0] != "off"))
            return new CoopCommandResult(false, "Usage: coop.debug.naval.result_silence [on | off]", "command_failed");

        var controller = Mission.Current?.GetMissionBehavior<CoopBattleController>();
        var session = controller?.Session;
        if (session?.IsNaval != true || !session.HasInstance)
        {
            if (args.Count == 1 && args[0] == "on")
                return new CoopCommandResult(false, "No active coop naval battle mission.", "command_failed");
            return new CoopCommandResult(true, "NAVAL_RESULT_SILENCE enabled=false instance=none; mission-scoped flag has expired");
        }

        if (args.Count == 1)
        {
            if (args[0] == "on" && session.IsLocalHost)
                return new CoopCommandResult(false, "Run on the non-host client before the host reports its result.", "command_failed");
            session.DebugWithholdCompletion = args[0] == "on";
            if (!session.DebugWithholdCompletion && controller.Deployment.IsCommitted)
                controller.ResultCommitter.ReportAcceptedResult();
        }

        return new CoopCommandResult(true, "NAVAL_RESULT_SILENCE enabled=" +
            (session.DebugWithholdCompletion ? "true" : "false") + " instance=" + session.InstanceId);
    }
}
#endif
