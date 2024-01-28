using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;

/// <summary>
/// Used to notify game interface to change value
/// </summary>
public record ChangeSettlementAlliesSpotted : ICommand
{
    public string SettlementId { get; }
    public float NumberOfAlliesSpottedAround { get; }

    public ChangeSettlementAlliesSpotted(string settlementId, float numberOfAlliesSpottedAround)
    {
        SettlementId = settlementId;
        NumberOfAlliesSpottedAround = numberOfAlliesSpottedAround;
    }
}
