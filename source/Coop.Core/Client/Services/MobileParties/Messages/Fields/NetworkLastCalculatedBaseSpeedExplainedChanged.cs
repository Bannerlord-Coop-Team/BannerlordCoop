using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

/// <summary>
/// Command to change the property _lastCalculatedBaseSpeedExplained of a mobile party.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkLastCalculatedBaseSpeedExplainedChanged(float Number, bool IncludeDescriptions, string TextObjectValue, string MobilePartyId) : ICommand
{
    [ProtoMember(1)]
    public float Number { get; } = Number;

    [ProtoMember(2)]
    public bool IncludeDescriptions { get; } = IncludeDescriptions;

    [ProtoMember(3)]
    public string TextObjectValue { get; } = TextObjectValue;

    [ProtoMember(4)]
    public string MobilePartyId { get; } = MobilePartyId;
}