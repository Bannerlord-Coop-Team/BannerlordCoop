using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkStartBattle : ICommand
{
    [ProtoMember(1)]
    public readonly string MapEventId;
    [ProtoMember(2)]
    public readonly string AttackerId;
    [ProtoMember(3)]
    public readonly string DefenderId;

    public NetworkStartBattle(string mapEventId, string attackerId, string defenderId)
    {
        MapEventId = mapEventId;
        AttackerId = attackerId;
        DefenderId = defenderId;
    }
}