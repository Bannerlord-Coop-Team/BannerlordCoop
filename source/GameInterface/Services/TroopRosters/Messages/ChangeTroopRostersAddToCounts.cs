using Common.Messaging;

namespace GameInterface.Services.TroopRosters.Messages;
public record ChangeTroopRostersAddToCounts : ICommand
{
    public string TroopRosterId { get; }
    public string ObjectId { get; }
    public bool IsHero { get; }
    public int Count { get; }

    public bool InsertAtFront { get; }
    public int WoundedCount { get; }
    public int XpChanged { get; }
    public bool RemoveDepleted { get; }
    public int Index { get; }


    public ChangeTroopRostersAddToCounts(
        string troopRosterId,
        string objectId,
        bool isHero,
        int count,
        bool insertAtFront,
        int woundedCount,
        int xpChanged,
        bool removeDepleted,
        int index)
    {
        TroopRosterId = troopRosterId;
        ObjectId = objectId;
        IsHero = isHero;
        Count = count;
        InsertAtFront = insertAtFront;
        WoundedCount = woundedCount;
        XpChanged = xpChanged;
        RemoveDepleted = removeDepleted;
        Index = index;
    }
}
