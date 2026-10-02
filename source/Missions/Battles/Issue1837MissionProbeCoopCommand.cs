#if DEBUG
using Common;
using Common.Commands;
using Newtonsoft.Json;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Battles;

public sealed class Issue1837MissionProbeCoopCommand : ICoopCommand
{
    public string Prefix => "coop.debug.siege";
    public string Name => "issue1837_mission_state";
    public string Description => "Reads the native siege mission result and elected authority without installing debug behaviors.";
    public CoopCommandSide Side => CoopCommandSide.Client;
    public IExpectedArgs[] ExpectedArgs { get; } = Array.Empty<IExpectedArgs>();

    public CoopCommandResult ProcessCommand(ICoopCommandArgs args)
    {
        var mission = Mission.Current;
        var controller = mission?.GetMissionBehavior<CoopBattleController>();
        if (ModInformation.IsServer || controller == null)
            return new CoopCommandResult(false, "No client coop mission is available.", "command_failed");
        return new CoopCommandResult(true, "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(new
        {
            controllerId = controller.Session.OwnControllerId,
            mapEventId = controller.Session.InstanceId,
            localAuthority = controller.Session.IsLocalHost,
            hostEpoch = controller.Session.HostEpoch,
            deploymentCommitted = controller.Deployment.IsCommitted,
            deploymentReady = mission.GetMissionBehavior<DeploymentMissionController>()?.TeamSetupOver,
            battleResolved = mission.MissionResult?.BattleResolved,
            battleState = mission.MissionResult?.BattleState.ToString(),
        }));
    }
}
#endif
