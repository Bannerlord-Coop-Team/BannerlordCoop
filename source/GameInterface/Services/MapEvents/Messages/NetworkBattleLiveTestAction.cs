#if DEBUG
using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages;

public enum BattleLiveTestAction
{
    StartAttackMission,
    FinishDeployment,
    WoundPlayer,
    KillEnemyTeam,
    LeaveBattle,
    FinishEncounter,
}

[ProtoContract]
public readonly struct NetworkBattleLiveTestAction : ICommand
{
    [ProtoMember(1)]
    public readonly BattleLiveTestAction Action;

    public NetworkBattleLiveTestAction(BattleLiveTestAction action)
    {
        Action = action;
    }
}
#endif
