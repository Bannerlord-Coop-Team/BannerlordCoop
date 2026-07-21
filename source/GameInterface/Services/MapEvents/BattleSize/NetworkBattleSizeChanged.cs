using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.BattleSize;

/// <summary>Updates clients with the server's battle-size setting.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkBattleSizeChanged : IEvent
{
    [ProtoMember(1)]
    public readonly int BattleSize;

    public NetworkBattleSizeChanged(int battleSize)
    {
        BattleSize = battleSize;
    }
}
