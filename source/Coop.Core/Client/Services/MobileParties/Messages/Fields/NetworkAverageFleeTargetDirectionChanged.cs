using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

/// <summary>
/// Client publish for _partyTradeGold
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkAverageFleeTargetDirectionChanged(float VecX, float VecY, string MobilePartyId) : ICommand
{
    [ProtoMember(1)]
    public float VecX { get; } = VecX;
    [ProtoMember(2)]
    public float VecY { get; } = VecY;
    [ProtoMember(3)]
    public string MobilePartyId { get; } = MobilePartyId;
}