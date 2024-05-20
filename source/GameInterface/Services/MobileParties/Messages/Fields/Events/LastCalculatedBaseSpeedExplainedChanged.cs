using Common.Messaging;
using GameInterface.Services.MobileParties.Messages.Fields.Commands;

namespace GameInterface.Services.MobileParties.Messages.Fields.Events;

/// <summary>
/// Client publish for _lastCalculatedBaseSpeedExplained
/// </summary>
public record LastCalculatedBaseSpeedExplainedChanged(float Number, bool IncludeDescriptions, string TextObjectValue, string MobilePartyId) : IEvent
{
    public float Number { get; } = Number;

    public bool IncludeDescriptions { get; } = IncludeDescriptions;
    
    public string TextObjectValue { get; } = TextObjectValue;
    
    public string MobilePartyId { get; } = MobilePartyId;
}