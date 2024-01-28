using Common.Messaging;

namespace GameInterface.Services.Settlements.Messages;


/// <summary>
/// Happens when Settlement.NumberOfAlliesSpottedAround changes.
/// </summary>
public record SettlementChangeAlliesSpotted : ICommand
{
    public string SettlementId { get; }
    public float NumberOfAlliesSpottedAround { get; }

    public SettlementChangeAlliesSpotted(string settlementId, float numberOfAlliesSpottedAround)
    {
        SettlementId = settlementId;
        NumberOfAlliesSpottedAround = numberOfAlliesSpottedAround;
    }
}
