using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.MapEvents.Messages.Start;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkStartBattle : ICommand
{
    [ProtoMember(1)]
    public readonly string AttackerId;
    [ProtoMember(2)]
    public readonly string DefenderId;

    public NetworkStartBattle(string attackerId, string defenderId)
    {
        AttackerId = attackerId;
        DefenderId = defenderId;
    }
}