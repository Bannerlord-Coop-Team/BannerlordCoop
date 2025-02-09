using Common.Messaging;
using System;
using System.Collections.Generic;
using System.Text;

namespace GameInterface.Services.TroopRosters.Messages;
public record ChangeTroopRostersAddToCounts : IEvent
{
    public string MobilePartyId { get; }
    public string Character { get; }

    public int Count { get; }

    public bool InsertAtFront { get; }
    public int WoundedCount { get; }
    public int xpChanged { get; }
    public bool RemoveDepleted { get; }
    public int Index { get; }

    public ChangeTroopRostersAddToCounts(string mobilePartyId, string character, int count, bool insertAtFront, int woundedCount, int xpChanged, bool removeDepleted, int index)
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
