using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.TroopRosters.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkChangeTroopRosterAddtoCounts : IEvent
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;

    [ProtoMember(2)]
    public readonly string CharacterId;

    [ProtoMember(3)]
    public readonly int Count;

    [ProtoMember(4)]
    public readonly bool InsertAtFront;

    [ProtoMember(5)]
    public readonly int WoundedCount;

    [ProtoMember(6)]
    public readonly int XpChanged;

    [ProtoMember(7)]
    public readonly bool RemoveDepleted;

    [ProtoMember(8)]
    public readonly int Index;

    public NetworkChangeTroopRosterAddtoCounts(
        string mobilePartyId,
        string character,
        int count,
        bool insertAtFront,
        int woundedCount,
        int xpChanged,
        bool removeDepleted,
        int index)
    {
        MobilePartyId = mobilePartyId;
        CharacterId = character;
        Count = count;
        InsertAtFront = insertAtFront;
        WoundedCount = woundedCount;
        XpChanged = xpChanged;
        RemoveDepleted = removeDepleted;
        Index = index;
    }
}
