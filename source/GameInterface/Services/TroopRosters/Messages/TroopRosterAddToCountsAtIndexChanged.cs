using Common.Messaging;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Messages;
public record TroopRosterAddToCountsAtIndexChanged : ICommand
{
    public TroopRoster TroopRoster { get; }
    public int Index { get; }
    public int Count { get; }
    public int WoundedCount { get; }
    public int XpChanged { get; }
    public bool RemoveDepleted { get; }

    public TroopRosterAddToCountsAtIndexChanged(TroopRoster troopRoster, int index, int count , int woundedCount, int xpChanged, bool removeDepleted)
    {
        TroopRoster = troopRoster;
        Index = index;
        Count = count;
        WoundedCount = woundedCount;
        XpChanged = xpChanged;
        RemoveDepleted = removeDepleted;
    }
}
