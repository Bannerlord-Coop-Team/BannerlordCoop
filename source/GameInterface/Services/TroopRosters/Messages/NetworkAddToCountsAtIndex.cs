using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.TroopRosters.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkAddToCountsAtIndex : ICommand
{
    [ProtoMember(1)]
    public readonly string TroopRosterId;

    [ProtoMember(2)]
    public readonly string ObjectId;

    [ProtoMember(3)]
    public readonly bool IsHero;

    [ProtoMember(4)]
    public readonly int CountChange;

    [ProtoMember(5)]
    public readonly int WoundedCountChange;

    [ProtoMember(6)]
    public readonly int XpChange;

    [ProtoMember(7)]
    public readonly bool RemoveDepleted;

    public NetworkAddToCountsAtIndex(string troopRosterId, string objectId, bool isHero, int countChange, int woundedCountChange, int xpChange, bool removeDepleted)
    {
        TroopRosterId = troopRosterId;
        ObjectId = objectId;
        IsHero = isHero;
        CountChange = countChange;
        WoundedCountChange = woundedCountChange;
        XpChange = xpChange;
        RemoveDepleted = removeDepleted;
    }
}
