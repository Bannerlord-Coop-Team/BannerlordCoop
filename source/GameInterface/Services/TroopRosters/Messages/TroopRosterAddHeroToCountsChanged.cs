using Common;
using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Messages;
public readonly struct TroopRosterAddHeroToCountsChanged : ICommand
{
    public readonly TroopRoster TroopRoster;
    public readonly Hero Hero;

    public readonly int Count;

    public readonly bool InsertAtFront;
    public readonly int WoundedCount;
    public readonly int XpChanged;
    public readonly bool RemoveDepleted;
    public readonly int Index;

    public TroopRosterAddHeroToCountsChanged(
        TroopRoster troopRoster,
        Hero hero,
        int count,
        bool insertAtFront,
        int woundedCount,
        int xpChanged,
        bool removeDepleted,
        int index)
    {
        TroopRoster = troopRoster;
        Hero = hero;
        Count = count;
        InsertAtFront = insertAtFront;
        WoundedCount = woundedCount;
        XpChanged = xpChanged;
        RemoveDepleted = removeDepleted;
        Index = index;
    }
}
