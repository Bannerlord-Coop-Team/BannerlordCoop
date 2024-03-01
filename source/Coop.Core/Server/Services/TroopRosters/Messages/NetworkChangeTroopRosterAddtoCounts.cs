using Common.Messaging;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Server.Services.TroopRosters.Messages;

[ProtoContract(SkipConstructor = true)]
public record NetworkChangeTroopRosterAddtoCounts : IEvent
{
    [ProtoMember(1)]
    public string MobilePartyId { get; }
    [ProtoMember(2)]
    public string Character { get; }

    [ProtoMember(3)]
    public int Count { get; }

    [ProtoMember(4)]
    public bool InsertAtFront { get; }

    [ProtoMember(5)]
    public int WoundedCount { get; }

    [ProtoMember(6)]
    public int xpChanged { get; }

    [ProtoMember(7)]
    public bool RemoveDepleted { get; }
    [ProtoMember(8)]
    public int Index { get; }

    public NetworkChangeTroopRosterAddtoCounts(string mobilePartyId, string character, int count, bool insertAtFront, int woundedCount, int xpChanged, bool removeDepleted, int index)
    {
        MobilePartyId = mobilePartyId;
        Character = character;
        Count = count;
        InsertAtFront = insertAtFront;
        WoundedCount = woundedCount;
        this.xpChanged = xpChanged;
        RemoveDepleted = removeDepleted;
        Index = index;
    }
}
