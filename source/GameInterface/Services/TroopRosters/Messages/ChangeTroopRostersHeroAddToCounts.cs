using Common.Messaging;

namespace GameInterface.Services.TroopRosters.Messages;
public readonly struct ChangeTroopRostersHeroAddToCounts : ICommand
{
    public readonly string TroopRosterId;
    public readonly string HeroId;
    public readonly int Count;
    public readonly bool InsertAtFront;
    public readonly int WoundedCount;
    public readonly int XpChanged;
    public readonly bool RemoveDepleted;
    public readonly int Index;

    public ChangeTroopRostersHeroAddToCounts(
        string troopRosterId,
        string heroId,
        int count,
        bool insertAtFront,
        int woundedCount,
        int xpChanged,
        bool removeDepleted,
        int index)
    {
        TroopRosterId = troopRosterId;
        HeroId = heroId;
        Count = count;
        InsertAtFront = insertAtFront;
        WoundedCount = woundedCount;
        XpChanged = xpChanged;
        RemoveDepleted = removeDepleted;
        Index = index;
    }
}
