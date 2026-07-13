using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Messages;

[ProtoContract]
public readonly struct NetworkChangeBattleState : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;
    [ProtoMember(2)]
    public readonly BattleState BattleState;
    [ProtoMember(3)]
    public readonly bool IsLeavingFallback;

    public NetworkChangeBattleState(string mapEventId, BattleState battleState, bool isLeavingFallback)
    {
        MapEventId = mapEventId;
        BattleState = battleState;
        IsLeavingFallback = isLeavingFallback;
    }
}
