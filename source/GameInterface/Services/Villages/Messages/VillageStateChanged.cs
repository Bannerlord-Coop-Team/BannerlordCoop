using Common.Messaging;

namespace GameInterface.Services.Villages.Messages;

/// <summary>
/// This is used when the server needs to update the VillageStates to the client
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
