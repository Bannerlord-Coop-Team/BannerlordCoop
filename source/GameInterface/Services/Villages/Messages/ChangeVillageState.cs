using Common.Messaging;

namespace GameInterface.Services.Villages.Messages;


/// <summary>
/// Updates the village state
/// </summary>
public record ChangeVillageState : ICommand
{
    public string VillageId { get; }
    public int State { get; }

    public ChangeVillageState(string settlementId, int state)
    {
        VillageId = settlementId;
        State = state;
    }
}