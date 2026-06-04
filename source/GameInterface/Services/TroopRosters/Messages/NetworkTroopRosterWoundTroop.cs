using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.TroopRosters.Messages;

[ProtoContract]
internal readonly struct NetworkTroopRosterWoundTroop : ICommand
{
    [ProtoMember(1)]
    public readonly string TroopRosterId;
    [ProtoMember(2)]
    public readonly string ObjectId;
    [ProtoMember(3)]
    public readonly bool IsHero;
    [ProtoMember(4)]
    public readonly int NumberToWound;

    public NetworkTroopRosterWoundTroop(string troopRosterId, string objectId, bool isHero, int numberToWound)
    {
        TroopRosterId = troopRosterId;
        ObjectId = objectId;
        IsHero = isHero;
        NumberToWound = numberToWound;
    }
}
