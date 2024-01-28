using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Settlements.Messages;


/// <summary>
/// Sends client information of number of allies spotted changed.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkChangeSettlementAlliesSpotted : IEvent
{
    [ProtoMember(1)]
    public string SettlementId { get; }
    [ProtoMember(2)]
    public float NumberOfAlliesSpottedAround { get; }

    public NetworkChangeSettlementAlliesSpotted(string settlementId, float numberOfAlliesSpottedAround)
    {
        SettlementId = settlementId;
        NumberOfAlliesSpottedAround = numberOfAlliesSpottedAround;
    }
}
