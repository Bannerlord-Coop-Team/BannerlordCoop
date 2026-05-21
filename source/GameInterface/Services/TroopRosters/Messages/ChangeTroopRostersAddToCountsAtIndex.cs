using Common.Messaging;

namespace GameInterface.Services.TroopRosters.Messages;
public record ChangeTroopRostersAddToCountsAtIndex : IEvent
{
    public string MobilePartyId { get; }
    public int Index { get; }
    public int Count { get; }
    public int WoundedCount { get; }
    public int XpChanged { get; }
    public bool RemoveDepleted { get; }


    public ChangeTroopRostersAddToCountsAtIndex(string mobilePartyId, int index, int count, int woundedCount, int xpChanged, bool removeDepleted)
    {
        MobilePartyId = mobilePartyId;
        Index = index;
        Count = count;
        WoundedCount = woundedCount;
        XpChanged = xpChanged;
        RemoveDepleted = removeDepleted;

    }
}
