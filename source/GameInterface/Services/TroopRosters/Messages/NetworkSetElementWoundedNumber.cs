using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.TroopRosters.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkSetElementWoundedNumber : ICommand
{
    [ProtoMember(1)]
    public readonly string TroopRosterId;

    [ProtoMember(2)]
    public readonly string ObjectId;

    [ProtoMember(3)]
    public readonly bool IsHero;

    [ProtoMember(4)]
    public readonly int Number;

    public NetworkSetElementWoundedNumber(string troopRosterId, string objectId, bool isHero, int number)
    {
        TroopRosterId = troopRosterId;
        ObjectId = objectId;
        IsHero = isHero;
        Number = number;
    }
}
