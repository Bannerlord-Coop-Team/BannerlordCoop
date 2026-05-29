using Common;
using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.TroopRosters.Messages;
public readonly struct TroopRosterAddToCountsChanged : ICommand
{
    public readonly TroopRoster TroopRoster;
    public readonly CharacterObject Troop;

    public readonly int Count;

    public readonly bool InsertAtFront;
    public readonly int WoundedCount;
    public readonly int XpChanged;
    public readonly bool RemoveDepleted;
    public readonly int Index;

    public TroopRosterAddToCountsChanged(
        TroopRoster troopRoster,
        CharacterObject troop,
        int count,
        bool insertAtFront,
        int woundedCount,
        int xpChanged,
        bool removeDepleted,
        int index)
    {
        TroopRoster = troopRoster;
        Troop = troop;
        Count = count;
        InsertAtFront = insertAtFront;
        WoundedCount = woundedCount;
        XpChanged = xpChanged;
        RemoveDepleted = removeDepleted;
        Index = index;
    }
}
