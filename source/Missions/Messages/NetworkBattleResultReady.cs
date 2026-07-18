using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;

namespace Missions.Messages;

/// <summary>
/// [Client to server] Reports that one member's battle mission reached a resolved result. The server reconciles
/// reports against its current mission membership before applying the shared campaign result.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkBattleResultReady : ICommand
{
    [ProtoMember(1)]
    public readonly string InstanceId;
    [ProtoMember(2)]
    public readonly BattleState BattleState;

    public NetworkBattleResultReady(string instanceId, BattleState battleState)
    {
        InstanceId = instanceId;
        BattleState = battleState;
    }
}
