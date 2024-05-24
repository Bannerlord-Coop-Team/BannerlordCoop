using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Client.Services.MobileParties.Messages.Fields;

/// <summary>
/// Command to change the property _lastCalculatedBaseSpeedExplained of a mobile party.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record NetworkLastCalculatedBaseSpeedExplainedChanged(float Number, bool IncludeDescriptions, string TextObjectValue, string MobilePartyId) : ICommand
{
    public float Number { get; } = Number;

    public bool IncludeDescriptions { get; } = IncludeDescriptions;
    
    public string TextObjectValue { get; } = TextObjectValue;
    public string MobilePartyId { get; } = MobilePartyId;
}