using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.TroopRosters.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkChangeTroopRosterAddToCounts : IEvent
{
    [ProtoMember(1)]
    public readonly string TroopRosterId;

    [ProtoMember(2)]
    public readonly string ObjectId;

    [ProtoMember(3)]
    public readonly bool IsHero;

    [ProtoMember(4)]
    public readonly int Count;

    [ProtoMember(5)]
    public readonly bool InsertAtFront;

    [ProtoMember(6)]
    public readonly int WoundedCount;

    [ProtoMember(7)]
    public readonly int XpChanged;

    [ProtoMember(8)]
    public readonly bool RemoveDepleted;

    [ProtoMember(9)]
    public readonly int Index;

    public NetworkChangeTroopRosterAddToCounts(
        string troopRosterId,
        string objectId,
        bool isHero,
        int count,
        bool insertAtFront,
        int woundedCount,
        int xpChanged,
        bool removeDepleted,
        int index)
    {
        TroopRosterId = troopRosterId;
        ObjectId = objectId;
        IsHero = isHero;
        Count = count;
        InsertAtFront = insertAtFront;
        WoundedCount = woundedCount;
        XpChanged = xpChanged;
        RemoveDepleted = removeDepleted;
        Index = index;
    }
}
