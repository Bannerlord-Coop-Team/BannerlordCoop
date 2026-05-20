using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.TroopRosters.Messages;

[ProtoContract(SkipConstructor = true)]
public record NetworkChangeTroopRosterAddtoCountsAtIndex : IEvent
{
    [ProtoMember(1)]
    public string MobilePartyId { get; }
    [ProtoMember(2)]
    public int Index { get; }

    [ProtoMember(3)]
    public int Count { get; }

    [ProtoMember(4)]
    public int WoundedCount { get; }

    [ProtoMember(5)]
    public int XpChanged { get; }

    [ProtoMember(6)]
    public bool RemoveDepleted { get; }

    public NetworkChangeTroopRosterAddtoCountsAtIndex(string mobilePartyId, int index, int count, int woundedCount, int xpChanged, bool removeDepleted)
    {
        MobilePartyId = mobilePartyId;
        Index = index;
        Count = count;
        WoundedCount = woundedCount;
        XpChanged = xpChanged;
        RemoveDepleted = removeDepleted;
    }
}
