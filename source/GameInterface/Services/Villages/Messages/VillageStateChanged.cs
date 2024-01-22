using Common.Messaging;

namespace GameInterface.Services.Villages.Messages;

/// <summary>
/// This event is fired when the village state is updated
/// </summary>
public record VillageStateChanged : ICommand
{
    public string SettlementId { get; }
    public int State { get; }

    public VillageStateChanged(string settlementId, int state)
    {
        SettlementId = settlementId;
        State = state;
    }
}
