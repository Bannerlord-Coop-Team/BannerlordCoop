using Common;
using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.TroopRosters.Messages;
public readonly struct TroopRosterAddToCountsChanged : ICommand
{
    public readonly MobileParty MobileParty;
    public readonly CharacterObject CharacterObject;

    public readonly int Count;

    public readonly bool InsertAtFront;
    public readonly int WoundedCount;
    public readonly int XpChanged;
    public readonly bool RemoveDepleted;
    public readonly int Index;

    public TroopRosterAddToCountsChanged(
        MobileParty mobileParty,
        CharacterObject characterObject,
        int count,
        bool insertAtFront,
        int woundedCount,
        int xpChanged,
        bool removeDepleted,
        int index)
    {
        MobileParty = mobileParty;
        CharacterObject = characterObject;
        Count = count;
        InsertAtFront = insertAtFront;
        WoundedCount = woundedCount;
        XpChanged = xpChanged;
        RemoveDepleted = removeDepleted;
        Index = index;
    }
}
