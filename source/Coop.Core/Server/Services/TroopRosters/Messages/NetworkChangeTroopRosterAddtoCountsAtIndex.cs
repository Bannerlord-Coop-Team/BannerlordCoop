using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.TroopRosters.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkChangeTroopRosterAddtoCountsAtIndex : IEvent
{
    [ProtoMember(1)]
    public readonly string TroopRosterId;
    [ProtoMember(2)]
    public readonly int Index;

    [ProtoMember(3)]
    public readonly int Count;

    [ProtoMember(4)]
    public readonly int WoundedCount;

    [ProtoMember(5)]
    public readonly int XpChanged;

    [ProtoMember(6)]
    public readonly bool RemoveDepleted;

    public NetworkChangeTroopRosterAddtoCountsAtIndex(
        string troopRosterId,
        int index,
        int count,
        int woundedCount,
        int xpChanged,
        bool removeDepleted)
    {
        TroopRosterId = troopRosterId;
        Index = index;
        Count = count;
        WoundedCount = woundedCount;
        XpChanged = xpChanged;
        RemoveDepleted = removeDepleted;
    }
}
