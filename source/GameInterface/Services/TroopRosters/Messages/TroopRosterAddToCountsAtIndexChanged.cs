using Common;
using Common.Messaging;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Messages;
public readonly struct TroopRosterAddToCountsAtIndexChanged : ICommand
{
    public readonly TroopRoster TroopRoster;
    public readonly int Index;
    public readonly int Count;
    public readonly int WoundedCount;
    public readonly int XpChanged;
    public readonly bool RemoveDepleted;

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
