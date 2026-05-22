using Common.Messaging;

namespace GameInterface.Services.TroopRosters.Messages;
public record ChangeTroopRostersAddToCounts : ICommand
{
    public string TroopRosterId { get; }
    public string Character { get; }

    public int Count { get; }

    public bool InsertAtFront { get; }
    public int WoundedCount { get; }
    public int xpChanged { get; }
    public bool RemoveDepleted { get; }
    public int Index { get; }

    public ChangeTroopRostersAddToCounts(string troopRosterId, string character, int count, bool insertAtFront, int woundedCount, int xpChanged, bool removeDepleted, int index)
    {
        TroopRosterId = troopRosterId;
        Character = character;
        Count = count;
        InsertAtFront = insertAtFront;
        WoundedCount = woundedCount;
        this.xpChanged = xpChanged;
        RemoveDepleted = removeDepleted;
        Index = index;
    }
}
