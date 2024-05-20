using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

/// <summary>
/// Client publish for _partyTradeGold
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkAverageFleeTargetDirectionChanged(float VecX, float VecY, string MobilePartyId) : ICommand
{
    public float VecX { get; } = VecX;
    public float VecY { get; } = VecY;
    public string MobilePartyId { get; } = MobilePartyId;
}